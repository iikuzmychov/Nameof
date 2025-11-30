using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Nameof;

[Generator]
internal sealed class NameofGenerator : IIncrementalGenerator
{
    private readonly record struct Request(
        INamedTypeSymbol? Symbol,
        string? FullTypeName,
        INamedTypeSymbol? InAssemblyOfType);

    private static readonly DiagnosticDescriptor UnsupportedFullTypeNameDescriptor = new(
        id: "NAMEOF001",
        title: "Unsupported full type name",
        messageFormat: "GenerateNameof(\"{0}\") is not supported. Only non-generic, non-nested full type names are supported (example: \"Namespace.Type\").",
        category: "NameofGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RuntimeTypeResolutionFailedDescriptor = new(
        id: "NAMEOF002",
        title: "Runtime type resolution failed",
        messageFormat: "Could not resolve runtime type \"{0}\" using inAssemblyOf \"{1}\". No members were generated.",
        category: "NameofGenerator",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("Nameof.Core.g.cs", SourceText.From(CoreSource, Encoding.UTF8));
        });

        var pipeline = context.CompilationProvider
            .Select(static (compilation, _) => WithAllMetadata(compilation))
            .Select(static (compilation, _) => (Compilation: compilation, Requests: CollectRequests(compilation)));

        context.RegisterSourceOutput(pipeline, static (spc, input) =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var request in input.Requests)
            {
                if (request.Symbol is not null)
                {
                    var key = request.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var source = GenerateForSymbol(request.Symbol);
                    if (!string.IsNullOrWhiteSpace(source))
                    {
                        spc.AddSource($"Nameof.{GetTypeIdentity(request.Symbol)}.g.cs", SourceText.From(source, Encoding.UTF8));
                    }

                    continue;
                }

                if (request.FullTypeName is null || request.InAssemblyOfType is null)
                {
                    continue;
                }

                if (!IsSupportedFullTypeName(request.FullTypeName))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedFullTypeNameDescriptor,
                        Location.None,
                        request.FullTypeName));

                    continue;
                }

                var stringKey = $"{request.InAssemblyOfType.ContainingAssembly.Identity.Name}|{request.FullTypeName}";
                if (!seen.Add(stringKey))
                {
                    continue;
                }

                var source2 = GenerateForFullTypeName(
                    spc,
                    input.Compilation,
                    request.FullTypeName,
                    request.InAssemblyOfType);

                if (!string.IsNullOrWhiteSpace(source2))
                {
                    spc.AddSource($"Nameof.{MakeId(stringKey)}.g.cs", SourceText.From(source2, Encoding.UTF8));
                }
            }
        });
    }

    private static Compilation WithAllMetadata(Compilation compilation)
    {
        if (compilation is not CSharpCompilation csharpCompilation)
        {
            return compilation;
        }

        var options = (CSharpCompilationOptions)csharpCompilation.Options;
        if (options.MetadataImportOptions == MetadataImportOptions.All)
        {
            return compilation;
        }

        return csharpCompilation.WithOptions(options.WithMetadataImportOptions(MetadataImportOptions.All));
    }

    private static ImmutableArray<Request> CollectRequests(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<Request>();

        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not INamedTypeSymbol attributeClass)
            {
                continue;
            }

            if (!string.Equals(attributeClass.Name, "GenerateNameofAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            if (attributeClass.Arity == 1 && attributeClass.TypeArguments.Length == 1)
            {
                if (attributeClass.TypeArguments[0] is INamedTypeSymbol typeArgument)
                {
                    builder.Add(new Request(typeArgument, null, null));
                }

                continue;
            }

            if (attributeClass.Arity != 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 1)
            {
                var arg0 = attribute.ConstructorArguments[0];
                if (arg0.Kind == TypedConstantKind.Type && arg0.Value is INamedTypeSymbol typeSymbol)
                {
                    builder.Add(new Request(typeSymbol, null, null));
                }

                continue;
            }

            if (attribute.ConstructorArguments.Length == 2)
            {
                var arg0 = attribute.ConstructorArguments[0];
                var arg1 = attribute.ConstructorArguments[1];

                if (arg0.Kind == TypedConstantKind.Primitive &&
                    arg0.Value is string fullTypeName &&
                    arg1.Kind == TypedConstantKind.Type &&
                    arg1.Value is INamedTypeSymbol inAssemblyOfType)
                {
                    var resolved = compilation.GetTypeByMetadataName(fullTypeName);

                    builder.Add(resolved is not null
                        ? new Request(resolved, null, null)
                        : new Request(null, fullTypeName, inAssemblyOfType));
                }
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsSupportedFullTypeName(string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullTypeName))
        {
            return false;
        }

        if (fullTypeName.Contains("`", StringComparison.Ordinal))
        {
            return false;
        }

        if (fullTypeName.Contains("+", StringComparison.Ordinal))
        {
            return false;
        }

        if (fullTypeName.Contains("[", StringComparison.Ordinal) || fullTypeName.Contains("]", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string GenerateForFullTypeName(
        SourceProductionContext context,
        Compilation compilation,
        string fullTypeName,
        INamedTypeSymbol inAssemblyOfType)
    {
        var (namespaceName, typeName) = SplitNamespaceAndTypeName(fullTypeName);
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        var runtimeType = TryResolveRuntimeType(fullTypeName, inAssemblyOfType);

        HashSet<string>? memberNames = null;
        if (runtimeType is not null)
        {
            memberNames = ExtractNonPublicMemberNames(runtimeType);
        }
        else
        {
            memberNames = TryExtractNonPublicMemberNamesFromRuntimeAssemblyFile(fullTypeName, inAssemblyOfType);
        }

        if (memberNames is null || memberNames.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RuntimeTypeResolutionFailedDescriptor,
                Location.None,
                fullTypeName,
                inAssemblyOfType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

            return string.Empty;
        }

        var hasSymbolInCompilation = compilation.GetTypeByMetadataName(fullTypeName) is not null;
        var stub = GetStubKind(runtimeType);

        var writer = new CodeWriter();
        writer.Line("#nullable enable");
        writer.Line();

        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            writer.OpenBlock($"namespace {namespaceName}");
        }

        if (!hasSymbolInCompilation)
        {
            writer.OpenBlock($"internal{stub.SealedKeyword} {stub.TypeKeyword} {typeName}");
            if (stub.NeedsPrivateConstructor)
            {
                writer.Line($"private {typeName}() {{ }}");
            }

            writer.CloseBlock();
            writer.Line();
        }

        var wrapperClassName = "Nameof_" + MakeId(fullTypeName);

        writer.OpenBlock($"internal static class {wrapperClassName}");
        writer.OpenBlock($"extension(global::Nameof.nameof<global::{fullTypeName}>)");

        foreach (var (identifier, value) in BuildMemberMap(memberNames).OrderBy(m => m.Identifier, StringComparer.Ordinal))
        {
            writer.Line($"public static string {identifier} => \"{EscapeStringLiteral(value)}\";");
        }

        writer.CloseBlock();
        writer.CloseBlock();

        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            writer.CloseBlock();
        }

        return writer.ToString();
    }

    private static string GenerateForSymbol(INamedTypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace is { IsGlobalNamespace: false }
            ? type.ContainingNamespace.ToDisplayString()
            : null;

        var nonPublicMemberNames = TryGetNonPublicMemberNamesViaReflection(type) ?? ExtractNonPublicMemberNames(type);
        if (nonPublicMemberNames.Count == 0)
        {
            return string.Empty;
        }

        var needsStub = type.DeclaredAccessibility != Accessibility.Public && !type.Locations.Any(l => l.IsInSource);
        var stub = GetStubKind(type);
        var wrapperClassName = "Nameof_" + GetTypeIdentity(type);
        var fullyQualifiedType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var writer = new CodeWriter();
        writer.Line("#nullable enable");
        writer.Line();

        if (!string.IsNullOrWhiteSpace(containingNamespace))
        {
            writer.OpenBlock($"namespace {containingNamespace}");
        }

        if (needsStub)
        {
            var typeParameters = FormatTypeParameters(type);
            writer.OpenBlock($"internal{stub.SealedKeyword} {stub.TypeKeyword} {type.Name}{typeParameters}");
            if (stub.NeedsPrivateConstructor)
            {
                writer.Line($"private {type.Name}() {{ }}");
            }

            writer.CloseBlock();
            writer.Line();
        }

        writer.OpenBlock($"internal static class {wrapperClassName}");
        writer.OpenBlock($"extension(global::Nameof.nameof<{fullyQualifiedType}>)");

        foreach (var (identifier, value) in BuildMemberMap(nonPublicMemberNames).OrderBy(m => m.Identifier, StringComparer.Ordinal))
        {
            writer.Line($"public static string {identifier} => \"{EscapeStringLiteral(value)}\";");
        }

        writer.CloseBlock();
        writer.CloseBlock();

        if (!string.IsNullOrWhiteSpace(containingNamespace))
        {
            writer.CloseBlock();
        }

        return writer.ToString();
    }

    private static string FormatTypeParameters(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0)
        {
            return string.Empty;
        }

        return "<" + string.Join(", ", type.TypeParameters.Select(p => p.Name)) + ">";
    }

    private static HashSet<string> ExtractNonPublicMemberNames(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            if (member.DeclaredAccessibility == Accessibility.Public)
            {
                continue;
            }

            var name = member switch
            {
                IFieldSymbol field when field.AssociatedSymbol is null => field.Name,
                IPropertySymbol property => property.Name,
                IEventSymbol @event => @event.Name,
                IMethodSymbol method when method.MethodKind == MethodKind.Ordinary => method.Name,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("<", StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static (string? Namespace, string TypeName) SplitNamespaceAndTypeName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        if (lastDot < 0)
        {
            return (null, fullTypeName);
        }

        return (fullTypeName[..lastDot], fullTypeName[(lastDot + 1)..]);
    }

    private static (string TypeKeyword, string SealedKeyword, bool NeedsPrivateConstructor) GetStubKind(Type? runtimeType)
    {
        if (runtimeType is null)
        {
            return ("class", " sealed", true);
        }

        if (runtimeType.IsEnum)
        {
            return ("enum", "", false);
        }

        if (runtimeType.IsInterface)
        {
            return ("interface", "", false);
        }

        if (runtimeType.IsValueType)
        {
            return ("struct", "", false);
        }

        return ("class", " sealed", true);
    }

    private static (string TypeKeyword, string SealedKeyword, bool NeedsPrivateConstructor) GetStubKind(INamedTypeSymbol type)
    {
        return type.TypeKind switch
        {
            TypeKind.Enum => ("enum", "", false),
            TypeKind.Interface => ("interface", "", false),
            TypeKind.Struct => ("struct", "", false),
            _ => ("class", " sealed", true)
        };
    }

#pragma warning disable RS1035
    private static Type? TryResolveRuntimeType(string fullTypeName, INamedTypeSymbol inAssemblyOfType)
    {
        try
        {
            var assemblyName = inAssemblyOfType.ContainingAssembly.Identity.Name;
            var inAssemblyOfFullName = inAssemblyOfType
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty);

            var whereRuntimeType =
                Type.GetType($"{inAssemblyOfFullName}, {assemblyName}", throwOnError: false) ??
                TryGetLoadedAssembly(assemblyName)?.GetType(inAssemblyOfFullName, throwOnError: false);

            var targetAssembly = whereRuntimeType?.Assembly ?? TryGetLoadedAssembly(assemblyName);

            var resolved = targetAssembly?.GetType(fullTypeName, throwOnError: false);
            if (resolved is not null)
            {
                return resolved;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var candidate = assembly.GetType(fullTypeName, throwOnError: false);
                if (candidate is not null)
                {
                    return candidate;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Assembly? TryGetLoadedAssembly(string assemblyName)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));

        if (loaded is not null)
        {
            return loaded;
        }

        try
        {
            return Assembly.Load(assemblyName);
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string>? TryGetNonPublicMemberNamesViaReflection(INamedTypeSymbol typeSymbol)
    {
        try
        {
            var fullTypeName = typeSymbol
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty);

            var assemblyName = typeSymbol.ContainingAssembly.Identity.Name;

            var runtimeType =
                Type.GetType($"{fullTypeName}, {assemblyName}", throwOnError: false) ??
                TryGetLoadedAssembly(assemblyName)?.GetType(fullTypeName, throwOnError: false);

            return runtimeType is null ? null : ExtractNonPublicMemberNames(runtimeType);
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string>? TryExtractNonPublicMemberNamesFromRuntimeAssemblyFile(string fullTypeName, INamedTypeSymbol inAssemblyOfType)
    {
        try
        {
            var assemblyName = inAssemblyOfType.ContainingAssembly.Identity.Name;
            var assemblyPath = TryResolveRuntimeAssemblyPath(assemblyName);
            if (assemblyPath is null)
            {
                return null;
            }

            using var stream = System.IO.File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);

            if (!peReader.HasMetadata)
            {
                return null;
            }

            return ExtractNonPublicMemberNames(peReader.GetMetadataReader(), fullTypeName);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveRuntimeAssemblyPath(string assemblyName)
    {
        try
        {
            var loaded = TryGetLoadedAssembly(assemblyName);
            if (loaded is not null && !string.IsNullOrWhiteSpace(loaded.Location) && System.IO.File.Exists(loaded.Location))
            {
                return loaded.Location;
            }

            var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (string.IsNullOrWhiteSpace(runtimeDir))
            {
                return null;
            }

            var direct = System.IO.Path.Combine(runtimeDir, assemblyName + ".dll");
            if (System.IO.File.Exists(direct))
            {
                return direct;
            }

            var dotnetRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(runtimeDir));
            if (string.IsNullOrWhiteSpace(dotnetRoot))
            {
                return null;
            }

            var sharedDir = System.IO.Path.Combine(dotnetRoot, "shared");
            if (!System.IO.Directory.Exists(sharedDir))
            {
                return null;
            }

            foreach (var frameworkDir in System.IO.Directory.GetDirectories(sharedDir))
            {
                var versions = System.IO.Directory.GetDirectories(frameworkDir);
                Array.Sort(versions);
                Array.Reverse(versions);

                foreach (var versionDir in versions)
                {
                    var candidate = System.IO.Path.Combine(versionDir, assemblyName + ".dll");
                    if (System.IO.File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
#pragma warning restore RS1035

    private static HashSet<string> ExtractNonPublicMemberNames(Type type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
        {
            AddIfRelevant(field.Name, names);
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
        {
            AddIfRelevant(property.Name, names);
        }

        foreach (var @event in type.GetEvents(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
        {
            AddIfRelevant(@event.Name, names);
        }

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (!method.IsSpecialName)
            {
                AddIfRelevant(method.Name, names);
            }
        }

        return names;

        static void AddIfRelevant(string name, HashSet<string> set)
        {
            if (!name.StartsWith("<", StringComparison.Ordinal))
            {
                set.Add(name);
            }
        }
    }

    private static HashSet<string>? ExtractNonPublicMemberNames(MetadataReader reader, string fullTypeName)
    {
        try
        {
            foreach (var typeDefHandle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(typeDefHandle);

                var ns = reader.GetString(typeDef.Namespace);
                var name = reader.GetString(typeDef.Name);

                var candidate = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                if (!string.Equals(candidate, fullTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                var result = new HashSet<string>(StringComparer.Ordinal);

                foreach (var fieldHandle in typeDef.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    if ((field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public)
                    {
                        continue;
                    }

                    var fieldName = reader.GetString(field.Name);
                    if (!fieldName.StartsWith("<", StringComparison.Ordinal))
                    {
                        result.Add(fieldName);
                    }
                }

                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if ((method.Attributes & MethodAttributes.SpecialName) != 0)
                    {
                        continue;
                    }

                    if ((method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                    {
                        continue;
                    }

                    var methodName = reader.GetString(method.Name);
                    if (!methodName.StartsWith("<", StringComparison.Ordinal))
                    {
                        result.Add(methodName);
                    }
                }

                foreach (var propertyHandle in typeDef.GetProperties())
                {
                    var property = reader.GetPropertyDefinition(propertyHandle);
                    var propertyName = reader.GetString(property.Name);
                    if (propertyName.StartsWith("<", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var accessors = property.GetAccessors();
                    if (IsAnyAccessorPublic(reader, accessors.Getter, accessors.Setter))
                    {
                        continue;
                    }

                    result.Add(propertyName);
                }

                foreach (var eventHandle in typeDef.GetEvents())
                {
                    var @event = reader.GetEventDefinition(eventHandle);
                    var eventName = reader.GetString(@event.Name);
                    if (eventName.StartsWith("<", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var accessors = @event.GetAccessors();
                    if (IsAnyAccessorPublic(reader, accessors.Adder, accessors.Remover))
                    {
                        continue;
                    }

                    result.Add(eventName);
                }

                return result;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAnyAccessorPublic(MetadataReader reader, MethodDefinitionHandle first, MethodDefinitionHandle second)
    {
        return (!first.IsNil && IsPublic(reader, first)) || (!second.IsNil && IsPublic(reader, second));
    }

    private static bool IsPublic(MetadataReader reader, MethodDefinitionHandle methodHandle)
    {
        var method = reader.GetMethodDefinition(methodHandle);
        return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
    }

    private static IReadOnlyList<(string Identifier, string Value)> BuildMemberMap(IEnumerable<string> memberNames)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string Identifier, string Value)>();

        foreach (var value in memberNames.Where(static n => !string.IsNullOrWhiteSpace(n)))
        {
            var identifier = ToSafeIdentifier(value);
            identifier = EnsureUnique(identifier, used);
            result.Add((identifier, value));
        }

        return result;
    }

    private static string ToSafeIdentifier(string memberName)
    {
        if (SyntaxFacts.IsValidIdentifier(memberName))
        {
            return IsKeyword(memberName) ? "@" + memberName : memberName;
        }

        var builder = new StringBuilder(memberName.Length + 8);

        if (memberName.Length == 0 || !IsIdentifierStart(memberName[0]))
        {
            builder.Append('_');
        }

        foreach (var ch in memberName)
        {
            builder.Append(IsIdentifierPart(ch) ? ch : '_');
        }

        var candidate = builder.ToString();
        if (SyntaxFacts.IsValidIdentifier(candidate))
        {
            return IsKeyword(candidate) ? "@" + candidate : candidate;
        }

        return "_" + candidate;
    }

    private static bool IsKeyword(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;
    }

    private static bool IsIdentifierStart(char ch)
    {
        return ch == '_' || char.IsLetter(ch);
    }

    private static bool IsIdentifierPart(char ch)
    {
        return ch == '_' || char.IsLetterOrDigit(ch);
    }

    private static string EnsureUnique(string identifier, HashSet<string> used)
    {
        if (used.Add(identifier))
        {
            return identifier;
        }

        for (var i = 2; ; i++)
        {
            var candidate = identifier + "_" + i;
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetTypeIdentity(INamedTypeSymbol type)
    {
        var builder = new StringBuilder();

        if (type.ContainingNamespace is { IsGlobalNamespace: false })
        {
            builder.Append(MakeId(type.ContainingNamespace.ToDisplayString()));
            builder.Append('_');
        }

        var stack = new Stack<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.ContainingType)
        {
            stack.Push(current);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            builder.Append(MakeId(current.MetadataName.Replace('`', '_')));
            builder.Append('_');
        }

        return builder.ToString().TrimEnd('_');
    }

    private static string MakeId(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        return builder.ToString();
    }

    private sealed class CodeWriter
    {
        private readonly StringBuilder _builder = new();
        private int _indent;

        public void Line()
        {
            _builder.AppendLine();
        }

        public void Line(string text)
        {
            if (text.Length == 0)
            {
                _builder.AppendLine();
                return;
            }

            _builder.Append(' ', _indent * 4);
            _builder.AppendLine(text);
        }

        public void OpenBlock(string header)
        {
            Line(header);
            Line("{");
            _indent++;
        }

        public void CloseBlock()
        {
            _indent = Math.Max(0, _indent - 1);
            Line("}");
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }

    private const string CoreSource = """
#nullable enable

namespace Nameof
{
    public static class nameof<T>
    {
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class GenerateNameofAttribute : global::System.Attribute
    {
        public GenerateNameofAttribute(global::System.Type type) { }
        public GenerateNameofAttribute(string fullTypeName, global::System.Type inAssemblyOf) { }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class GenerateNameofAttribute<T> : global::System.Attribute
    {
    }
}
""";
}
