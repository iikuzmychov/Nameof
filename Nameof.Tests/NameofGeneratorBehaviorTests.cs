using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nameof.Tests.TestInfrastructure;

namespace Nameof.Tests;

public class NameofGeneratorBehaviorTests
{
    [Fact]
    public Task Generates_nameof_for_current_assembly_internal_struct()
    {
        var source =
            """
            using Nameof;

            [assembly: GenerateNameof(typeof(SomeStruct))]

            internal struct SomeStruct
            {
                private int _value;
                private void Reset() { }
            }
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_current_assembly_internal_interface()
    {
        var source =
            """
            using Nameof;

            [assembly: GenerateNameof(typeof(ISomeContract))]

            internal interface ISomeContract
            {
                int Count { get; }
                void Execute();
            }
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_current_assembly_internal_class()
    {
        const string source =
            """
            using Nameof;

            [assembly: GenerateNameof(typeof(SomeType))]

            internal class SomeType
            {
                private int _someField;
                private void SomeMethod() { }
                private string SomeProperty { get; set; } = "";
            }
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_external_assembly_public_class_with_private_fields()
    {
        const string source =
            """
            using System;
            using Nameof;

            [assembly: GenerateNameof<ConsoleKeyInfo>]
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_external_assembly_internal_class_using_assembly_of()
    {
        const string source =
            """
            using System;
            using Nameof;

            [assembly: GenerateNameof("System.IO.ConsoleStream", assemblyOf: typeof(Console))]
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_external_assembly_internal_class_using_assembly_name()
    {
        const string source =
            """
            using Nameof;

            [assembly: GenerateNameof("Nameof.NameofGenerator", assemblyName: "Nameof")]
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_external_assembly_internal_enum_using_assembly_name()
    {
        var targetFixture = CreateExternalReferenceAssembly(
            assemblyName: "Task3.ExternalEnumFixture",
            source:
            """
            namespace ExternalFixtures;

            internal enum HiddenEnum
            {
                First,
                Second
            }
            """);
        var decoyFixture = CreateExternalReferenceAssembly(
            assemblyName: "Task3.ExternalEnumFixture.Decoy",
            source:
            """
            namespace ExternalFixtures;

            internal sealed class HiddenEnum
            {
                internal const string Unexpected = "Unexpected";
            }
            """);

        var source =
            """
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.HiddenEnum", assemblyName: "Task3.ExternalEnumFixture")]
            """;

        var result = GeneratorTestDriver.Run(source, targetFixture.Reference, decoyFixture.Reference);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_external_assembly_internal_struct_using_assembly_name()
    {
        var targetFixture = CreateExternalReferenceAssembly(
            assemblyName: "Task3.ExternalStructFixture",
            source:
            """
            namespace ExternalFixtures;

            internal struct HiddenStruct
            {
                internal int Value;
                internal void Reset() { }
            }
            """);
        var decoyFixture = CreateExternalReferenceAssembly(
            assemblyName: "Task3.ExternalStructFixture.Decoy",
            source:
            """
            namespace ExternalFixtures;

            internal sealed class HiddenStruct
            {
                internal static int Unexpected => 42;
            }
            """);

        var source =
            """
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.HiddenStruct", assemblyName: "Task3.ExternalStructFixture")]
            """;

        var result = GeneratorTestDriver.Run(source, targetFixture.Reference, decoyFixture.Reference);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_external_assembly_internal_interface_using_assembly_of()
    {
        var targetFixture = CreateExternalReferenceAssembly(
            assemblyName: "Task3.ExternalInterfaceFixture",
            source:
            """
            namespace ExternalFixtures;

            internal interface IHiddenContract
            {
                int Count { get; }
                void Execute();
            }

            public sealed class InterfaceAssemblyAnchor;
            """);
        var decoyFixture = CreateExternalReferenceAssembly(
            assemblyName: "Task3.ExternalInterfaceFixture.Decoy",
            source:
            """
            namespace ExternalFixtures;

            internal sealed class IHiddenContract
            {
                internal void Unexpected() { }
            }
            """);

        var source =
            """
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.IHiddenContract", assemblyOf: typeof(ExternalFixtures.InterfaceAssemblyAnchor))]
            """;

        var result = GeneratorTestDriver.Run(source, targetFixture.Reference, decoyFixture.Reference);

        return Verify(result.ToSnapshot());
    }

    private static ExternalReferenceFixture CreateExternalReferenceAssembly(string assemblyName, string source)
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

        return new ExternalReferenceFixture(
            MetadataReference.CreateFromImage(image),
            assemblyName);
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

    private sealed record ExternalReferenceFixture(
        MetadataReference Reference,
        string AssemblyName);
}
