using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nameof.Tests.TestInfrastructure;

namespace Nameof.Tests;

public class NameofGeneratorBehaviorTests
{
    [Theory]
    [CombinatorialData]
    public Task Generates_nameof_behavior(
        AssemblyType assemblyType,
        LookupKind lookupKind,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var testCase = BuildBehaviorCase(assemblyType, lookupKind, declarationType, accessType);
        var result = GeneratorTestDriver.Run(testCase.Source, testCase.References);

        return Verify(result.ToSnapshot()).UseTextForParameters(testCase.SnapshotName);
    }

    private static BehaviorCase BuildBehaviorCase(
        AssemblyType assemblyType,
        LookupKind lookupKind,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var snapshotName = $"{assemblyType}_{lookupKind}_{declarationType}_{accessType}";

        return (assemblyType, lookupKind) switch
        {
            (AssemblyType.CurrentAssembly, LookupKind.ByType)
                => BuildCurrentAssemblyCase(snapshotName, LookupKind.ByType, declarationType, accessType),

            (AssemblyType.CurrentAssembly, LookupKind.ByAssemblyName)
                => BuildCurrentAssemblyCase(snapshotName, LookupKind.ByAssemblyName, declarationType, accessType),

            (AssemblyType.CurrentAssembly, LookupKind.ByAssemblyOf)
                => BuildCurrentAssemblyCase(snapshotName, LookupKind.ByAssemblyOf, declarationType, accessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByType)
                => BuildExternalAssemblyByTypeCase(snapshotName, declarationType, accessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyName)
                => BuildExternalAssemblyByAssemblyNameCase(snapshotName, declarationType, accessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyOf)
                => BuildExternalAssemblyByAssemblyOfCase(snapshotName, declarationType, accessType),

            _ => throw new ArgumentOutOfRangeException(nameof(lookupKind)),
        };
    }

    private static BehaviorCase BuildCurrentAssemblyCase(
        string snapshotName,
        LookupKind lookupKind,
        DeclarationType declarationType,
        AccessType accessType)
    {
        return CreateBehaviorCase(snapshotName, CreateCurrentAssemblySource(lookupKind, declarationType, accessType));
    }

    private static BehaviorCase BuildExternalAssemblyByTypeCase(
        string snapshotName,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var fixtureName = $"ByType{declarationType}{accessType}";
        var metadataName = $"ExternalFixtures.{fixtureName}";

        var fixture = CreateExternalFixture(
            assemblyName: $"Behavior.External.ByType.{declarationType}.{accessType}",
            declarationType,
            accessType,
            typeName: fixtureName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof<ExternalFixtures.{{fixtureName}}>]
            """;

        return CreateBehaviorCase(snapshotName, source, fixture.Reference);
    }

    private static BehaviorCase BuildExternalAssemblyByAssemblyNameCase(
        string snapshotName,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var fixtureName = $"ByAssemblyName{declarationType}{accessType}";

        var assemblyName = $"Behavior.External.ByAssemblyName.{declarationType}.{accessType}";

        var fixture = CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            typeName: fixtureName);

        var decoy = CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            typeName: fixtureName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.{{fixtureName}}", assemblyName: "{{assemblyName}}")]
            """;

        return CreateBehaviorCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static BehaviorCase BuildExternalAssemblyByAssemblyOfCase(
        string snapshotName,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var fixtureName = $"ByAssemblyOf{declarationType}{accessType}";
        var anchorName = $"ByAssemblyOf{declarationType}{accessType}Anchor";
        var assemblyName = $"Behavior.External.ByAssemblyOf.{declarationType}.{accessType}";

        var fixture = CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            typeName: fixtureName,
            includeAnchor: true,
            anchorTypeName: anchorName);

        var decoy = CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            typeName: fixtureName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.{{fixtureName}}", assemblyOf: typeof(ExternalFixtures.{{anchorName}}))]
            """;

        return CreateBehaviorCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static BehaviorCase CreateBehaviorCase(
        string snapshotName,
        string source,
        params MetadataReference[] references)
    {
        return new BehaviorCase(snapshotName, source, references);
    }

    public enum AssemblyType
    {
        CurrentAssembly,
        ExternalAssembly,
    }

    public enum LookupKind
    {
        ByType,
        ByAssemblyName,
        ByAssemblyOf,
    }

    public enum DeclarationType
    {
        Class,
        Struct,
        Interface,
        Enum,
    }

    public enum AccessType
    {
        Public,
        Internal,
    }

    private sealed record BehaviorCase(
        string SnapshotName,
        string Source,
        MetadataReference[] References);

    private static MetadataReference CreateExternalReferenceAssembly(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            GetExternalFixtureReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));

        var image = peStream.ToArray();
        Assembly.Load(image);

        return MetadataReference.CreateFromImage(image);
    }

    private static ExternalFixture CreateExternalFixture(
        string assemblyName,
        DeclarationType declarationType,
        AccessType accessType,
        string typeName,
        bool includeAnchor = false,
        string? anchorTypeName = null)
    {
        var source = CreateExternalDeclarationAssemblySource(
            declarationType,
            accessType,
            typeName,
            includeAnchor,
            anchorTypeName);

        return new ExternalFixture(
            Reference: CreateExternalReferenceAssembly(assemblyName, source),
            TypeName: typeName,
            AssemblyName: assemblyName);
    }

    private static MetadataReference CreateDecoyFixture(
        string assemblyName,
        DeclarationType declarationType,
        string typeName)
    {
        var declaration = declarationType switch
        {
            DeclarationType.Class =>
                $$"""
                internal class {{typeName}}
                {
                    internal int Unexpected { get; }
                }
                """,

            DeclarationType.Struct =>
                $$"""
                internal struct {{typeName}}
                {
                    internal int Unexpected { get; }
                }
                """,

            DeclarationType.Interface =>
                $$"""
                internal interface {{typeName}}
                {
                    int Unexpected { get; }
                }
                """,

            DeclarationType.Enum =>
                $$"""
                internal enum {{typeName}}
                {
                    Unexpected
                }
                """,

            _ => throw new ArgumentOutOfRangeException(nameof(declarationType)),
        };

        var source =
            $$"""
            namespace ExternalFixtures;

            {{declaration}}
            """;

        return CreateExternalReferenceAssembly(assemblyName, source);
    }

    private static string CreateCurrentAssemblySource(
        LookupKind lookupKind,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var typeName = $"CurrentAssembly{accessType}{declarationType}";
        var anchorTypeName = $"{typeName}Anchor";

        var visibility = accessType switch
        {
            AccessType.Public => "public",
            AccessType.Internal => "internal",
            _ => throw new ArgumentOutOfRangeException(nameof(accessType)),
        };

        var declaration = declarationType switch
        {
            DeclarationType.Class => CreateClassDeclaration(visibility, typeName),
            DeclarationType.Struct => CreateStructDeclaration(visibility, typeName),
            DeclarationType.Interface => CreateInterfaceDeclaration(visibility, typeName),
            DeclarationType.Enum => CreateEnumDeclaration(visibility, typeName),
            _ => throw new ArgumentOutOfRangeException(nameof(declarationType)),
        };

        return lookupKind switch
        {
            LookupKind.ByType => CreateCurrentAssemblyByTypeSource(typeName, declaration),
            LookupKind.ByAssemblyName => CreateCurrentAssemblyByAssemblyNameSource(typeName, declaration),
            LookupKind.ByAssemblyOf => CreateCurrentAssemblyByAssemblyOfSource(typeName, anchorTypeName, declaration),
            _ => throw new ArgumentOutOfRangeException(nameof(lookupKind)),
        };
    }

    private static string CreateExternalDeclarationAssemblySource(
        DeclarationType declarationType,
        AccessType accessType,
        string typeName,
        bool includeAnchor,
        string? anchorTypeName)
    {
        var visibility = accessType switch
        {
            AccessType.Public => "public",
            AccessType.Internal => "internal",
            _ => throw new ArgumentOutOfRangeException(nameof(accessType)),
        };

        var internalVisibleToDeclaration = accessType is AccessType.Internal
            ? @$"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""{typeName}.Tests"")]"
            : string.Empty;

        var anchorDeclaration = includeAnchor
            ? @$"public sealed class {anchorTypeName} {{ }}"
            : string.Empty;

        var declaration = declarationType switch
        {
            DeclarationType.Class => CreateClassDeclaration(visibility, typeName),
            DeclarationType.Struct => CreateStructDeclaration(visibility, typeName),
            DeclarationType.Interface => CreateInterfaceDeclaration(visibility, typeName),
            DeclarationType.Enum => CreateEnumDeclaration(visibility, typeName),
            _ => throw new ArgumentOutOfRangeException(nameof(declarationType)),
        };

        return
            $$"""
            {{internalVisibleToDeclaration}}

            namespace ExternalFixtures;

            {{anchorDeclaration}}

            {{declaration}}
            """;
    }

    private static string CreateCurrentAssemblyByTypeSource(string typeOfExpression, string declaration)
    {
        return $$"""
        using Nameof;

        [assembly: GenerateNameof(typeof({{typeOfExpression}}))]

        {{declaration}}
        """;
    }

    private static string CreateCurrentAssemblyByAssemblyNameSource(string typeName, string declaration)
    {
        return $$"""
        using Nameof;

        [assembly: GenerateNameof("{{typeName}}", assemblyName: "GeneratorTests")]

        {{declaration}}
        """;
    }

    private static string CreateCurrentAssemblyByAssemblyOfSource(
        string typeName,
        string anchorTypeName,
        string declaration)
    {
        return $$"""
        using Nameof;

        [assembly: GenerateNameof("{{typeName}}", assemblyOf: typeof({{anchorTypeName}}))]

        public sealed class {{anchorTypeName}};

        {{declaration}}
        """;
    }

    private static string CreateClassDeclaration(string visibility, string typeName)
    {
        return
            $$"""
            {{visibility}} class {{typeName}}
            {
                private int _privateField;
                internal int _internalField;
                protected int _protectedField;
                protected internal int _protectedInternalField;
                private protected int _privateProtectedField;
                public int _publicField;

                private int PrivateProperty { get; set; }
                internal int InternalProperty { get; set; }
                protected int ProtectedProperty { get; set; }
                protected internal int ProtectedInternalProperty { get; set; }
                private protected int PrivateProtectedProperty { get; set; }
                public int PublicProperty { get; set; }

                private event global::System.Action? PrivateEvent;
                internal event global::System.Action? InternalEvent;
                protected event global::System.Action? ProtectedEvent;
                protected internal event global::System.Action? ProtectedInternalEvent;
                private protected event global::System.Action? PrivateProtectedEvent;
                public event global::System.Action? PublicEvent;

                private void PrivateMethod() { }
                internal void InternalMethod() { }
                protected void ProtectedMethod() { }
                protected internal void ProtectedInternalMethod() { }
                private protected void PrivateProtectedMethod() { }
                public void PublicMethod() { }
            }
            """;
    }

    private static string CreateStructDeclaration(string visibility, string typeName)
    {
        return
            $$"""
            {{visibility}} struct {{typeName}}
            {
                private int _privateField;
                internal int _internalField;
                public int _publicField;

                private int PrivateProperty { get; set; }
                internal int InternalProperty { get; set; }
                public int PublicProperty { get; set; }

                private event global::System.Action? PrivateEvent;
                internal event global::System.Action? InternalEvent;
                public event global::System.Action? PublicEvent;

                private void PrivateMethod() { }
                internal void InternalMethod() { }
                public void PublicMethod() { }
            }
            """;
    }

    private static string CreateInterfaceDeclaration(string visibility, string typeName)
    {
        return
            $$"""
            {{visibility}} interface {{typeName}}
            {
                int Count { get; }
                string Name { get; }
                event global::System.Action? Changed;
                void Execute();
            }
        """;
    }

    private static string CreateEnumDeclaration(string visibility, string typeName)
    {
        return
            $$"""
            {{visibility}} enum {{typeName}}
            {
                First,
                Second
            }
            """;
    }

    private static MetadataReference[] GetExternalFixtureReferences()
    {
        return AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies
            ? trustedPlatformAssemblies
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(static path => MetadataReference.CreateFromFile(path))
                .ToArray()
            : [];
    }

    private sealed record ExternalFixture(
        MetadataReference Reference,
        string TypeName,
        string AssemblyName);
}
