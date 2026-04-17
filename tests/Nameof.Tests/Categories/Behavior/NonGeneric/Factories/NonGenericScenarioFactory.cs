using Microsoft.CodeAnalysis;
using Nameof.Tests.Categories.Behavior.NonGeneric.Model;
using Nameof.Tests.Infrastructure.Model;

namespace Nameof.Tests.Categories.Behavior.NonGeneric.Factories;

internal static class NonGenericScenarioFactory
{
    internal static NonGenericScenarioCase BuildScenarioCase(
        AssemblyType assemblyType,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var snapshotName = $"{assemblyType}_{declarationType}_{accessType}";

        return assemblyType switch
        {
            AssemblyType.CurrentAssembly => new NonGenericScenarioCase
            {
                SnapshotName = snapshotName,
                ByType = BuildCase(assemblyType, LookupKind.ByType, declarationType, accessType),
                ByAssemblyName = BuildCase(assemblyType, LookupKind.ByAssemblyName, declarationType, accessType),
                ByAssemblyOf = BuildCase(assemblyType, LookupKind.ByAssemblyOf, declarationType, accessType),
            },

            AssemblyType.ExternalAssembly => BuildExternalAssemblyScenarioCase(snapshotName, declarationType, accessType),

            _ => throw new ArgumentOutOfRangeException(nameof(assemblyType))
        };
    }

    private static NonGenericCase BuildCase(
        AssemblyType assemblyType,
        LookupKind lookupKind,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var snapshotName = $"{assemblyType}_{lookupKind}_{declarationType}_{accessType}";

        return (assemblyType, lookupKind) switch
        {
            (AssemblyType.CurrentAssembly, _)
                => CreateCase(snapshotName, NonGenericSourceFactory.CreateCurrentAssemblySource(lookupKind, declarationType, accessType)),

            (AssemblyType.ExternalAssembly, LookupKind.ByType)
                => BuildExternalAssemblyByTypeCase(snapshotName, declarationType, accessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyName)
                => BuildExternalAssemblyByAssemblyNameCase(snapshotName, declarationType, accessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyOf)
                => BuildExternalAssemblyByAssemblyOfCase(snapshotName, declarationType, accessType),

            _ => throw new ArgumentOutOfRangeException(nameof(lookupKind)),
        };
    }

    private static NonGenericScenarioCase BuildExternalAssemblyScenarioCase(
        string snapshotName,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var typeName = $"{declarationType}{accessType}";
        var anchorName = $"{typeName}Anchor";
        var assemblyName = $"NonGeneric.External.{declarationType}.{accessType}";

        var fixture = NonGenericAssemblyFactory.CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            typeName,
            includeAnchor: true,
            anchorTypeName: anchorName);

        var decoy = NonGenericAssemblyFactory.CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            typeName);

        var byTypeSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof(typeof(ExternalFixtures.{{typeName}}))]
            """;

        var byAssemblyNameSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.{{typeName}}", assemblyName: "{{assemblyName}}")]
            """;

        var byAssemblyOfSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.{{typeName}}", assemblyOf: typeof(ExternalFixtures.{{anchorName}}))]
            """;

        return new NonGenericScenarioCase
        {
            SnapshotName = snapshotName,
            ByType = CreateCase($"{snapshotName}_ByType", byTypeSource, fixture.Reference),
            ByAssemblyName = CreateCase($"{snapshotName}_ByAssemblyName", byAssemblyNameSource, fixture.Reference, decoy),
            ByAssemblyOf = CreateCase($"{snapshotName}_ByAssemblyOf", byAssemblyOfSource, fixture.Reference, decoy),
        };
    }

    private static NonGenericCase BuildExternalAssemblyByTypeCase(
        string snapshotName,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var fixtureName = $"ByType{declarationType}{accessType}";

        var fixture = NonGenericAssemblyFactory.CreateExternalFixture(
            assemblyName: $"NonGeneric.External.ByType.{declarationType}.{accessType}",
            declarationType,
            accessType,
            typeName: fixtureName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof(typeof(ExternalFixtures.{{fixtureName}}))]
            """;

        return CreateCase(snapshotName, source, fixture.Reference);
    }

    private static NonGenericCase BuildExternalAssemblyByAssemblyNameCase(
        string snapshotName,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var fixtureName = $"ByAssemblyName{declarationType}{accessType}";
        var assemblyName = $"NonGeneric.External.ByAssemblyName.{declarationType}.{accessType}";

        var fixture = NonGenericAssemblyFactory.CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            typeName: fixtureName);

        var decoy = NonGenericAssemblyFactory.CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            typeName: fixtureName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.{{fixtureName}}", assemblyName: "{{assemblyName}}")]
            """;

        return CreateCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static NonGenericCase BuildExternalAssemblyByAssemblyOfCase(
        string snapshotName,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var fixtureName = $"ByAssemblyOf{declarationType}{accessType}";
        var anchorName = $"ByAssemblyOf{declarationType}{accessType}Anchor";
        var assemblyName = $"NonGeneric.External.ByAssemblyOf.{declarationType}.{accessType}";

        var fixture = NonGenericAssemblyFactory.CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            typeName: fixtureName,
            includeAnchor: true,
            anchorTypeName: anchorName);

        var decoy = NonGenericAssemblyFactory.CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            typeName: fixtureName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("ExternalFixtures.{{fixtureName}}", assemblyOf: typeof(ExternalFixtures.{{anchorName}}))]
            """;

        return CreateCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static NonGenericCase CreateCase(
        string snapshotName,
        string source,
        params MetadataReference[] references)
    {
        return new NonGenericCase
        {
            SnapshotName = snapshotName,
            Source = source,
            References = references,
        };
    }
}
