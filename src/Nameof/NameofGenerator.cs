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
    private readonly record struct TargetRequest(
        INamedTypeSymbol? Symbol,
        string? MetadataTypeName,
        INamedTypeSymbol? AssemblyWhereType
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("Nameof.Core.g.cs", SourceText.From(CoreSource, Encoding.UTF8)));

        var pipeline = context.CompilationProvider
            .Select(static (c, _) => EnableAllMetadata(c))
            .Select(static (compilation, _) => (compilation, requests: CollectRequests(compilation)));

        context.RegisterSourceOutput(pipeline, static (spc, input) =>
        {
            var compilation = input.compilation;
            var requests = input.requests;

            var processed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var req in requests)
            {
                if (req.Symbol != null)
                {
                    var key = req.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (!processed.Add(key))
                        continue;

                    var source = GenerateWrapperForType(req.Symbol);
                    if (!string.IsNullOrWhiteSpace(source))
                        spc.AddSource($"Nameof.{GetSafeFileName(req.Symbol)}.g.cs", SourceText.From(source, Encoding.UTF8));

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(req.MetadataTypeName) && req.AssemblyWhereType != null)
                {
                    var key = $"{req.MetadataTypeName}|{req.AssemblyWhereType.ContainingAssembly.Identity.Name}";
                    if (!processed.Add(key))
                        continue;

                    var source = GenerateWrapperForMetadataType(req.MetadataTypeName!, req.AssemblyWhereType, compilation);
                    if (!string.IsNullOrWhiteSpace(source))
                        spc.AddSource($"Nameof.{GetSafeFileName(req.MetadataTypeName!)}.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }
        });
    }

    private static Compilation EnableAllMetadata(Compilation compilation)
    {
        if (compilation is CSharpCompilation csharp)
        {
            var options = (CSharpCompilationOptions)csharp.Options;
            if (options.MetadataImportOptions != MetadataImportOptions.All)
                return csharp.WithOptions(options.WithMetadataImportOptions(MetadataImportOptions.All));
        }

        return compilation;
    }

    private static ImmutableArray<TargetRequest> CollectRequests(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<TargetRequest>();

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass is not INamedTypeSymbol attrClass)
                continue;

            if (attrClass.Name != "GenerateNameofAttribute")
                continue;

            if (attrClass.Arity == 1 && attrClass.TypeArguments.Length == 1)
            {
                if (attrClass.TypeArguments[0] is INamedTypeSymbol typeArg)
                    builder.Add(new TargetRequest(typeArg, null, null));

                continue;
            }

            if (attrClass.Arity != 0)
                continue;

            if (attr.ConstructorArguments.Length == 1)
            {
                var arg = attr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol ts)
                    builder.Add(new TargetRequest(ts, null, null));

                continue;
            }

            if (attr.ConstructorArguments.Length == 2)
            {
                var arg0 = attr.ConstructorArguments[0];
                var arg1 = attr.ConstructorArguments[1];

                if (arg0.Kind == TypedConstantKind.Primitive && arg0.Value is string metadataName &&
                    arg1.Kind == TypedConstantKind.Type && arg1.Value is INamedTypeSymbol assemblyWhereType)
                {
                    var resolved = FindTypeByName(compilation, metadataName);
                    if (resolved != null)
                        builder.Add(new TargetRequest(resolved, null, null));
                    else
                        builder.Add(new TargetRequest(null, metadataName, assemblyWhereType));
                }
            }
        }

        return builder.ToImmutable();
    }

    private static string GenerateWrapperForMetadataType(string fullTypeName, INamedTypeSymbol assemblyWhereType, Compilation compilation)
    {
        var (ns, typeName) = SplitNamespaceAndTypeName(fullTypeName);

        var runtimeType = TryResolveRuntimeType(fullTypeName, assemblyWhereType);
        var members = runtimeType != null
            ? ExtractNonPublicMembersFromType(runtimeType)
            : TryGetMembersFromRuntimeDll(fullTypeName, assemblyWhereType);

        if (members == null || members.Count == 0)
            return string.Empty;

        var (typeKeyword, sealedKeyword, needsPrivateCtor) = GetStubKind(runtimeType);

        var fq = "global::" + fullTypeName;
        var wrapperClassName = GetWrapperClassName(fullTypeName);

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var needsNamespace = !string.IsNullOrWhiteSpace(ns);
        var indent = needsNamespace ? "    " : "";

        if (needsNamespace)
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// This type was auto-generated for referencing non-accessible type within nameof expressions.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}internal{sealedKeyword} {typeKeyword} {typeName}");
        sb.AppendLine($"{indent}{{");
        if (needsPrivateCtor)
            sb.AppendLine($"{indent}    private {typeName}() {{ }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();

        sb.AppendLine($"{indent}internal static class {wrapperClassName}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    extension(global::Nameof.nameof<{fq}>)");
        sb.AppendLine($"{indent}    {{");
        foreach (var name in members.OrderBy(n => n))
            sb.AppendLine($"{indent}        public static string {name} => \"{name}\";");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");

        if (needsNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private static (string? ns, string typeName) SplitNamespaceAndTypeName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        if (lastDot < 0)
            return (null, fullTypeName);

        return (fullTypeName[..lastDot], fullTypeName[(lastDot + 1)..]);
    }

    private static (string typeKeyword, string sealedKeyword, bool needsPrivateCtor) GetStubKind(Type? runtimeType)
    {
        if (runtimeType == null)
            return ("class", " sealed", true);

        if (runtimeType.IsEnum)
            return ("enum", "", false);

        if (runtimeType.IsInterface)
            return ("interface", "", false);

        if (runtimeType.IsValueType)
            return ("struct", "", false);

        return ("class", " sealed", true);
    }

#pragma warning disable RS1035
    private static Type? TryResolveRuntimeType(string fullTypeName, INamedTypeSymbol assemblyWhereType)
    {
        try
        {
            var whereAsmName = assemblyWhereType.ContainingAssembly.Identity.Name;
            var whereTypeName = assemblyWhereType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty);

            Assembly? whereAsm = null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == whereAsmName)
                {
                    whereAsm = asm;
                    break;
                }
            }

            if (whereAsm == null)
            {
                try
                { whereAsm = Assembly.Load(whereAsmName); }
                catch { }
            }

            Type? whereRuntimeType = null;
            if (whereAsm != null)
            {
                try
                { whereRuntimeType = whereAsm.GetType(whereTypeName, throwOnError: false); }
                catch { }
            }

            if (whereRuntimeType == null)
            {
                try
                { whereRuntimeType = Type.GetType($"{whereTypeName}, {whereAsmName}", throwOnError: false); }
                catch { }
            }

            var targetAsm = whereRuntimeType?.Assembly ?? whereAsm;
            if (targetAsm != null)
            {
                var t = targetAsm.GetType(fullTypeName, throwOnError: false);
                if (t != null)
                    return t;
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullTypeName, throwOnError: false);
                    if (t != null)
                        return t;
                }
                catch { }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string>? TryGetMembersFromRuntimeDll(string fullTypeName, INamedTypeSymbol assemblyWhereType)
    {
        try
        {
            var asmName = assemblyWhereType.ContainingAssembly.Identity.Name;
            var dllPath = TryResolveRuntimeAssemblyPath(asmName);
            if (dllPath == null)
                return null;

            using var fileStream = System.IO.File.OpenRead(dllPath);
            using var peReader = new PEReader(fileStream);

            if (!peReader.HasMetadata)
                return null;

            var metadataReader = peReader.GetMetadataReader();
            return ExtractNonPublicMembersFromMetadata(metadataReader, fullTypeName);
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
            var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (string.IsNullOrEmpty(runtimeDir))
                return null;

            var direct = System.IO.Path.Combine(runtimeDir, $"{assemblyName}.dll");
            if (System.IO.File.Exists(direct))
                return direct;

            var dotnetRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(runtimeDir));
            if (string.IsNullOrEmpty(dotnetRoot))
                return null;

            var sharedDir = System.IO.Path.Combine(dotnetRoot, "shared");
            if (!System.IO.Directory.Exists(sharedDir))
                return null;

            foreach (var frameworkDir in System.IO.Directory.GetDirectories(sharedDir))
            {
                var versions = System.IO.Directory.GetDirectories(frameworkDir);
                Array.Sort(versions);
                Array.Reverse(versions);

                foreach (var versionDir in versions)
                {
                    var candidate = System.IO.Path.Combine(versionDir, $"{assemblyName}.dll");
                    if (System.IO.File.Exists(candidate))
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
#pragma warning restore RS1035

    private static string GenerateWrapperForType(INamedTypeSymbol type)
    {
        var fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var containingNs = type.ContainingNamespace is { IsGlobalNamespace: false }
            ? type.ContainingNamespace.ToDisplayString()
            : null;

        var wrapperClassName = GetWrapperClassName(type);
        var isTypeAccessible = type.DeclaredAccessibility == Accessibility.Public;
        var isInCurrentCompilation = type.Locations.Any(loc => loc.IsInSource);

        var memberNames = new HashSet<string>(StringComparer.Ordinal);

        var runtimeMembers = TryGetMembersViaReflection(type);
        if (runtimeMembers != null)
        {
            foreach (var m in runtimeMembers)
                memberNames.Add(m);
        }
        else
        {
            foreach (var member in type.GetMembers())
            {
                if (member.IsImplicitlyDeclared)
                    continue;

                if (member.DeclaredAccessibility == Accessibility.Public)
                    continue;

                string? name = member switch
                {
                    IFieldSymbol f when f.AssociatedSymbol == null => f.Name,
                    IPropertySymbol p => p.Name,
                    IEventSymbol e => e.Name,
                    IMethodSymbol m when m.MethodKind == MethodKind.Ordinary => m.Name,
                    _ => null
                };

                if (!string.IsNullOrEmpty(name) && !name.StartsWith("<", StringComparison.Ordinal))
                    memberNames.Add(name);
            }
        }

        if (memberNames.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var needsNamespace = !string.IsNullOrEmpty(containingNs);
        if (needsNamespace)
        {
            sb.AppendLine($"namespace {containingNs}");
            sb.AppendLine("{");
        }

        if (!isTypeAccessible && !isInCurrentCompilation)
        {
            var indent = needsNamespace ? "    " : "";

            var (typeKeyword, sealedKeyword, needsPrivateCtor) = type.TypeKind switch
            {
                TypeKind.Enum => ("enum", "", false),
                TypeKind.Interface => ("interface", "", false),
                TypeKind.Struct => ("struct", "", false),
                _ => ("class", " sealed", true)
            };

            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// This type was auto-generated for referencing non-accessible type within nameof expressions.");
            sb.AppendLine($"{indent}/// </summary>");
            sb.AppendLine($"{indent}internal{sealedKeyword} {typeKeyword} {type.Name}");
            sb.AppendLine($"{indent}{{");
            if (needsPrivateCtor)
                sb.AppendLine($"{indent}    private {type.Name}() {{ }}");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }

        var wrapperIndent = needsNamespace ? "    " : "";
        sb.AppendLine($"{wrapperIndent}internal static class {wrapperClassName}");
        sb.AppendLine($"{wrapperIndent}{{");
        sb.AppendLine($"{wrapperIndent}    extension(global::Nameof.nameof<{fullyQualifiedName}>)");
        sb.AppendLine($"{wrapperIndent}    {{");

        foreach (var name in memberNames.OrderBy(n => n))
            sb.AppendLine($"{wrapperIndent}        public static string {name} => \"{name}\";");

        sb.AppendLine($"{wrapperIndent}    }}");
        sb.AppendLine($"{wrapperIndent}}}");

        if (needsNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

#pragma warning disable RS1035
    private static HashSet<string>? TryGetMembersViaReflection(INamedTypeSymbol typeSymbol)
    {
        try
        {
            var fullTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty);

            var assemblyName = typeSymbol.ContainingAssembly.Identity.Name;

            var assemblyQualifiedName = $"{fullTypeName}, {assemblyName}";
            var runtimeType = Type.GetType(assemblyQualifiedName);
            if (runtimeType != null)
                return ExtractNonPublicMembersFromType(runtimeType);

            Assembly? targetAssembly = null;

            foreach (var loadedAsm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loadedAsm.GetName().Name == assemblyName)
                {
                    targetAssembly = loadedAsm;
                    break;
                }
            }

            if (targetAssembly == null)
            {
                try
                { targetAssembly = Assembly.Load(assemblyName); }
                catch { }
            }

            if (targetAssembly != null)
            {
                runtimeType = targetAssembly.GetType(fullTypeName);
                if (runtimeType != null)
                    return ExtractNonPublicMembersFromType(runtimeType);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
#pragma warning restore RS1035

    private static HashSet<string> ExtractNonPublicMembersFromType(Type type)
    {
        var members = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
            if (!field.Name.StartsWith("<", StringComparison.Ordinal))
                members.Add(field.Name);

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
            if (!prop.Name.StartsWith("<", StringComparison.Ordinal))
                members.Add(prop.Name);

        foreach (var evt in type.GetEvents(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
            if (!evt.Name.StartsWith("<", StringComparison.Ordinal))
                members.Add(evt.Name);

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
            if (!method.IsSpecialName && !method.Name.StartsWith("<", StringComparison.Ordinal))
                members.Add(method.Name);

        return members;
    }

    private static HashSet<string>? ExtractNonPublicMembersFromMetadata(MetadataReader metadataReader, string fullTypeName)
    {
        try
        {
            var memberNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                var typeNamespace = metadataReader.GetString(typeDef.Namespace);
                var typeName = metadataReader.GetString(typeDef.Name);

                var candidateFullName = string.IsNullOrEmpty(typeNamespace)
                    ? typeName
                    : $"{typeNamespace}.{typeName}";

                if (!string.Equals(candidateFullName, fullTypeName, StringComparison.Ordinal))
                    continue;

                foreach (var fieldHandle in typeDef.GetFields())
                {
                    var field = metadataReader.GetFieldDefinition(fieldHandle);
                    var fieldName = metadataReader.GetString(field.Name);

                    var access = field.Attributes & FieldAttributes.FieldAccessMask;
                    if (access == FieldAttributes.Public)
                        continue;

                    if (!fieldName.StartsWith("<", StringComparison.Ordinal))
                        memberNames.Add(fieldName);
                }

                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var method = metadataReader.GetMethodDefinition(methodHandle);
                    var methodName = metadataReader.GetString(method.Name);

                    if ((method.Attributes & MethodAttributes.SpecialName) != 0)
                        continue;

                    var access = method.Attributes & MethodAttributes.MemberAccessMask;
                    if (access == MethodAttributes.Public)
                        continue;

                    if (!methodName.StartsWith("<", StringComparison.Ordinal))
                        memberNames.Add(methodName);
                }

                foreach (var propertyHandle in typeDef.GetProperties())
                {
                    var property = metadataReader.GetPropertyDefinition(propertyHandle);
                    var propertyName = metadataReader.GetString(property.Name);

                    var accessors = property.GetAccessors();
                    var isPublic = false;

                    if (!accessors.Getter.IsNil)
                    {
                        var getter = metadataReader.GetMethodDefinition(accessors.Getter);
                        if ((getter.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                            isPublic = true;
                    }

                    if (!accessors.Setter.IsNil)
                    {
                        var setter = metadataReader.GetMethodDefinition(accessors.Setter);
                        if ((setter.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                            isPublic = true;
                    }

                    if (isPublic)
                        continue;

                    if (!propertyName.StartsWith("<", StringComparison.Ordinal))
                        memberNames.Add(propertyName);
                }

                foreach (var eventHandle in typeDef.GetEvents())
                {
                    var evt = metadataReader.GetEventDefinition(eventHandle);
                    var eventName = metadataReader.GetString(evt.Name);

                    var accessors = evt.GetAccessors();
                    var isPublic = false;

                    if (!accessors.Adder.IsNil)
                    {
                        var adder = metadataReader.GetMethodDefinition(accessors.Adder);
                        if ((adder.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                            isPublic = true;
                    }

                    if (!accessors.Remover.IsNil)
                    {
                        var remover = metadataReader.GetMethodDefinition(accessors.Remover);
                        if ((remover.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                            isPublic = true;
                    }

                    if (isPublic)
                        continue;

                    if (!eventName.StartsWith("<", StringComparison.Ordinal))
                        memberNames.Add(eventName);
                }

                return memberNames;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static INamedTypeSymbol? FindTypeByName(Compilation compilation, string fullTypeName)
        => compilation.GetTypeByMetadataName(fullTypeName);

    private static string GetWrapperClassName(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace is { IsGlobalNamespace: false }
            ? type.ContainingNamespace.ToDisplayString().Replace('.', '_') + "_"
            : string.Empty;

        return $"Nameof_{ns}{type.Name}";
    }

    private static string GetWrapperClassName(string metadataTypeName)
        => "Nameof_" + metadataTypeName.Replace('.', '_').Replace('+', '_').Replace('`', '_');

    private static string GetSafeFileName(INamedTypeSymbol type)
    {
        var nsPrefix = type.ContainingNamespace is { IsGlobalNamespace: false }
            ? type.ContainingNamespace.ToDisplayString().Replace('.', '_') + "."
            : string.Empty;

        return $"{nsPrefix}{type.Name}";
    }

    private static string GetSafeFileName(string metadataTypeName)
        => metadataTypeName.Replace('+', '.').Replace('`', '_');

    private const string CoreSource = @"#nullable enable

namespace Nameof
{
    public static class nameof<T>
    {
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class GenerateNameofAttribute : global::System.Attribute
    {
        public GenerateNameofAttribute(global::System.Type type) { }
        public GenerateNameofAttribute(string metadataTypeName, global::System.Type assemblyWhere) { }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class GenerateNameofAttribute<T> : global::System.Attribute
    {
    }
}
";
}
