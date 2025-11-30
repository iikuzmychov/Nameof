using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nameof.SourceGenerators;

[Generator]
internal sealed class NameofGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Nameof.Shared.GenerateNameofAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var annotatedTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Where(static symbol => IsEffectivelyPublicOrInternal(symbol));

        var accessibleTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                static (syntaxContext, _) =>
                {
                    var declaration = (TypeDeclarationSyntax)syntaxContext.Node;
                    return syntaxContext.SemanticModel.GetDeclaredSymbol(declaration) as INamedTypeSymbol;
                })
            .Where(static symbol => symbol is not null && IsEffectivelyPublicOrInternal(symbol));

        var hasAssemblyAttribute = context.CompilationProvider.Select(static (compilation, _) =>
        {
            try
            {
                return compilation.Assembly.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::" + AttributeFullName);
            }
            catch
            {
                return false;
            }
        });

        context.RegisterSourceOutput(
            accessibleTypes.Combine(hasAssemblyAttribute),
            static (outputContext, pair) =>
            {
                var symbol = pair.Left;
                var enabled = pair.Right;
                if (enabled)
                {
                    TryGenerate(outputContext, symbol);
                }
            });

        context.RegisterSourceOutput(
            annotatedTypes.Combine(hasAssemblyAttribute),
            static (outputContext, pair) =>
            {
                var symbol = pair.Left;
                var enabled = pair.Right;
                if (!enabled)
                {
                    TryGenerate(outputContext, symbol);
                }
            });
    }

    private static void TryGenerate(SourceProductionContext context, INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind is not TypeKind.Class and not TypeKind.Struct)
        {
            return;
        }

        try
        {
            var source = GenerateSource(symbol);
            if (!string.IsNullOrEmpty(source))
            {
                context.AddSource(GetSourceHintName(symbol), source);
            }
        }
        catch (Exception ex)
        {
            var descriptor = new DiagnosticDescriptor(
                "NOF001",
                "Nameof generator failure",
                "Failed generating nameof extension for '{0}': {1}",
                "SourceGenerator",
                DiagnosticSeverity.Warning,
                true);

            context.ReportDiagnostic(Diagnostic.Create(descriptor, symbol.Locations.FirstOrDefault(), symbol.Name, ex.Message));
        }
    }

    private static string GenerateSource(INamedTypeSymbol type)
    {
        var memberNames = type.GetMembers()
            .Where(IsSupportedPrivateMember)
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToArray();

        var hasNested = HasInaccessibleDescendants(type);
        if (memberNames.Length == 0 && !hasNested)
        {
            return string.Empty;
        }

        var visibility = ResolveWrapperVisibility(type);
        var wrapperName = GetWrapperClassName(type);
        var target = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var typeParams = GetTypeParameters(type);
        var whereClauses = GetTypeConstraints(type);

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Nameof.Shared;");
        sb.AppendLine();
        sb.AppendLine($"{visibility} static class {wrapperName}");
        sb.AppendLine("{");

        var header = type.Arity == 0
            ? $"extension(global::Nameof.Shared.nameof<{target}>)"
            : $"extension{typeParams}(global::Nameof.Shared.nameof<{target}>)" + whereClauses;

        sb.AppendLine(Indent(header));
        sb.AppendLine(Indent("{"));

        foreach (var name in memberNames)
        {
            sb.AppendLine(Indent(Indent($"public static string {name} => \"{name}\";")));
        }

        foreach (var nested in type.GetTypeMembers().OrderBy(t => t.Name))
        {
            if (!IsEffectivelyPublicOrInternal(nested))
            {
                var containerName = GetContainerName(new[] { nested });
                sb.AppendLine(Indent(Indent($"public static {containerName} {nested.Name} => new {containerName}();")));
            }
        }

        sb.AppendLine(Indent("}"));
        sb.AppendLine();

        var containers = CollectContainers(type);
        foreach (var c in containers.OrderBy(c => c.Name))
        {
            sb.AppendLine(Indent($"public sealed class {c.Name}"));
            sb.AppendLine(Indent("{"));

            foreach (var name in c.Members.OrderBy(x => x))
            {
                sb.AppendLine(Indent(Indent($"public string {name} => \"{name}\";")));
            }

            foreach (var child in c.Children.OrderBy(x => x.Symbol.Name))
            {
                sb.AppendLine(Indent(Indent($"public {child.Name} {child.Symbol.Name} => new {child.Name}();")));
            }

            sb.AppendLine(Indent(Indent($"public override string ToString() => \"{c.Symbol.Name}\";")));
            sb.AppendLine(Indent("}"));
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed class Container
    {
        public string Name { get; set; } = string.Empty;
        public INamedTypeSymbol Symbol { get; set; } = null!;
        public List<string> Members { get; } = new();
        public List<Container> Children { get; } = new();
    }

    private static List<Container> CollectContainers(INamedTypeSymbol root)
    {
        var map = new Dictionary<INamedTypeSymbol, Container>(SymbolEqualityComparer.Default);

        void Visit(INamedTypeSymbol current, List<INamedTypeSymbol> path, bool started)
        {
            var selfPrivate = !IsEffectivelyPublicOrInternal(current);
            var privateChain = started || selfPrivate;
            var newPath = new List<INamedTypeSymbol>(path) { current };

            if (privateChain)
            {
                var first = newPath.FindIndex(t => !IsEffectivelyPublicOrInternal(t));
                var chain = newPath.Skip(first >= 0 ? first : newPath.Count - 1).ToArray();
                var name = GetContainerName(chain);

                if (!map.TryGetValue(current, out var container))
                {
                    container = new Container { Name = name, Symbol = current };
                    map[current] = container;

                    if (selfPrivate)
                    {
                        foreach (var member in current.GetMembers().Where(IsSupportedPrivateMember).Select(m => m.Name).Distinct())
                        {
                            container.Members.Add(member);
                        }
                    }
                }
                else
                {
                    container.Name = name;
                }

                foreach (var childSymbol in current.GetTypeMembers())
                {
                    var childPrivate = !IsEffectivelyPublicOrInternal(childSymbol);
                    Visit(childSymbol, newPath, privateChain || childPrivate);

                    if (childPrivate && map.TryGetValue(childSymbol, out var childContainer))
                    {
                        if (!container.Children.Contains(childContainer))
                        {
                            container.Children.Add(childContainer);
                        }
                    }
                }
            }
            else
            {
                foreach (var childSymbol in current.GetTypeMembers())
                {
                    var childPrivate = !IsEffectivelyPublicOrInternal(childSymbol);
                    Visit(childSymbol, newPath, childPrivate);
                }
            }
        }

        foreach (var topSymbol in root.GetTypeMembers())
        {
            Visit(topSymbol, new List<INamedTypeSymbol>(), !IsEffectivelyPublicOrInternal(topSymbol));
        }

        return map.Values.ToList();
    }

    private static bool HasInaccessibleDescendants(INamedTypeSymbol symbol)
    {
        return symbol.GetTypeMembers().Any(child => !IsEffectivelyPublicOrInternal(child) || HasInaccessibleDescendants(child));
    }

    private static string GetContainerName(IEnumerable<INamedTypeSymbol> chain)
    {
        return string.Join("_", chain.Select(s => s.Name + (s.Arity > 0 ? "_Generated_" + s.Arity : string.Empty)));
    }

    private static bool IsEffectivelyPublicOrInternal(INamedTypeSymbol symbol)
    {
        if (symbol.DeclaredAccessibility is not Accessibility.Public and not Accessibility.Internal)
        {
            return false;
        }

        for (var parent = symbol.ContainingType; parent != null; parent = parent.ContainingType)
        {
            if (parent.DeclaredAccessibility is not Accessibility.Public and not Accessibility.Internal)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupportedPrivateMember(ISymbol member)
    {
        if (member.DeclaredAccessibility != Accessibility.Private || member.IsImplicitlyDeclared)
        {
            return false;
        }

        return member switch
        {
            IFieldSymbol => true,
            IPropertySymbol => true,
            IEventSymbol => true,
            IMethodSymbol method => method.MethodKind is not (MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor or MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise or MethodKind.BuiltinOperator or MethodKind.UserDefinedOperator),
            _ => false
        };
    }

    private static string ResolveWrapperVisibility(INamedTypeSymbol type)
    {
        var attribute = type.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::" + AttributeFullName);
        var desired = -1;

        if (attribute != null &&
            attribute.ConstructorArguments.Length == 1 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Enum)
        {
            desired = (int)attribute.ConstructorArguments[0].Value!;
        }

        if (type.DeclaredAccessibility != Accessibility.Public)
        {
            return "internal";
        }

        return desired == 1 ? "internal" : "public";
    }

    private static string GetWrapperClassName(INamedTypeSymbol type)
    {
        var namespacePrefix = type.ContainingNamespace is { IsGlobalNamespace: false }
            ? type.ContainingNamespace.ToDisplayString().Replace('.', '_') + "_"
            : string.Empty;

        var baseName = type.Name + (type.Arity > 0 ? "_Generated_" + type.Arity : string.Empty);
        return "Nameof_" + namespacePrefix + baseName;
    }

    private static string GetSourceHintName(INamedTypeSymbol type)
    {
        return GetWrapperClassName(type) + ".g.cs";
    }

    private static string GetTypeParameters(INamedTypeSymbol type)
    {
        return type.Arity == 0 ? string.Empty : "<" + string.Join(", ", type.TypeParameters.Select(tp => tp.Name)) + ">";
    }

    private static string GetTypeConstraints(INamedTypeSymbol type)
    {
        if (type.Arity == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var parameter in type.TypeParameters)
        {
            var parts = new List<string>();

            if (parameter.HasReferenceTypeConstraint)
            {
                parts.Add("class");
            }

            if (parameter.HasValueTypeConstraint)
            {
                parts.Add("struct");
            }

            if (parameter.HasUnmanagedTypeConstraint)
            {
                parts.Add("unmanaged");
            }

            if (parameter.HasNotNullConstraint)
            {
                parts.Add("notnull");
            }

            parts.AddRange(parameter.ConstraintTypes.Select(ct => ct.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

            if (parameter.HasConstructorConstraint)
            {
                parts.Add("new()");
            }

            if (parts.Count > 0)
            {
                sb.Append(" where ");
                sb.Append(parameter.Name);
                sb.Append(" : ");
                sb.Append(string.Join(", ", parts));
            }
        }

        return sb.ToString();
    }

    private static string Indent(string value)
    {
        return "    " + value;
    }
}
