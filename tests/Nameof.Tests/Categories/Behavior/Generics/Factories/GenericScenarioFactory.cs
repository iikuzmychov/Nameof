using Microsoft.CodeAnalysis;
using Nameof.Tests.Categories.Behavior.Generics.Model;
using Nameof.Tests.Infrastructure.Model;

namespace Nameof.Tests.Categories.Behavior.Generics.Factories;

internal static class GenericScenarioFactory
{
    internal static GenericScenarioCase BuildGenericScenarioCase(
        AssemblyType assemblyType,
        GenericDeclarationType declarationType,
        Arity arity,
        AccessType accessType)
    {
        var snapshotName = $"{assemblyType}_{declarationType}_{arity}_{accessType}";

        return assemblyType switch
        {
            AssemblyType.CurrentAssembly => CreateScenarioCase(
                snapshotName,
                lookupKind => BuildGenericCase(assemblyType, lookupKind, declarationType, arity, accessType)),

            AssemblyType.ExternalAssembly => BuildExternalAssemblyScenarioCase(snapshotName, declarationType, arity, accessType),

            _ => throw new ArgumentOutOfRangeException(nameof(assemblyType)),
        };
    }

    internal static GenericScenarioCase BuildCoexistenceScenarioCase(
        AssemblyType assemblyType,
        AccessType nonGenericAccessType,
        AccessType genericAccessType)
    {
        var snapshotName = $"{assemblyType}_SharedName_{nonGenericAccessType}_{genericAccessType}";

        return assemblyType switch
        {
            AssemblyType.CurrentAssembly => CreateScenarioCase(
                snapshotName,
                lookupKind => BuildCoexistenceCase(assemblyType, lookupKind, nonGenericAccessType, genericAccessType)),

            AssemblyType.ExternalAssembly => BuildExternalCoexistenceScenarioCase(snapshotName, nonGenericAccessType, genericAccessType),

            _ => throw new ArgumentOutOfRangeException(nameof(assemblyType)),
        };
    }

    private static GenericScenarioCase BuildExternalAssemblyScenarioCase(
        string snapshotName,
        GenericDeclarationType declarationType,
        Arity arity,
        AccessType accessType)
    {
        var typeName = $"{declarationType}{accessType}{arity}";
        var anchorName = $"{typeName}Anchor";
        var assemblyName = $"Generic.External.{declarationType}.{arity}.{accessType}";

        var fixture = GenericAssemblyFactory.CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            arity,
            typeName,
            includeAnchor: true,
            anchorTypeName: anchorName);

        var decoy = GenericAssemblyFactory.CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            accessType,
            arity,
            typeName);

        var metadataName = GenericArityUtilities.GetMetadataName($"GenericFixtures.{fixture.TypeName}", arity);

        var byTypeSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof(typeof(GenericFixtures.{{fixture.TypeName}}{{GenericArityUtilities.GetOpenGenericTypeArguments(arity)}}))]
            """;

        var byAssemblyNameSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("{{metadataName}}", assemblyName: "{{assemblyName}}")]
            """;

        var byAssemblyOfSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("{{metadataName}}", assemblyOf: typeof(GenericFixtures.{{anchorName}}))]
            """;

        return CreateScenarioCase(
            snapshotName,
            lookupKind => lookupKind switch
            {
                LookupKind.ByType => CreateCase($"{snapshotName}_ByType", byTypeSource, fixture.Reference),
                LookupKind.ByAssemblyName => CreateCase($"{snapshotName}_ByAssemblyName", byAssemblyNameSource, fixture.Reference, decoy),
                LookupKind.ByAssemblyOf => CreateCase($"{snapshotName}_ByAssemblyOf", byAssemblyOfSource, fixture.Reference, decoy),
                _ => throw new ArgumentOutOfRangeException(nameof(lookupKind)),
            });
    }

    private static GenericScenarioCase BuildExternalCoexistenceScenarioCase(
        string snapshotName,
        AccessType nonGenericAccessType,
        AccessType genericAccessType)
    {
        const string typeName = "SharedName";
        const string anchorName = "SharedNameAnchor";
        var assemblyName = $"Generic.External.Coexistence.{nonGenericAccessType}.{genericAccessType}";

        var fixture = GenericAssemblyFactory.CreateCoexistenceFixture(
            assemblyName,
            nonGenericAccessType,
            genericAccessType,
            typeName,
            includeAnchor: true,
            anchorTypeName: anchorName);

        var decoy = GenericAssemblyFactory.CreateCoexistenceDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            nonGenericAccessType,
            genericAccessType,
            typeName);

        var byTypeSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof(typeof(GenericFixtures.SharedName<>))]
            """;

        var byAssemblyNameSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("GenericFixtures.SharedName`1", assemblyName: "{{assemblyName}}")]
            """;

        var byAssemblyOfSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("GenericFixtures.SharedName`1", assemblyOf: typeof(GenericFixtures.{{anchorName}}))]
            """;

        return CreateScenarioCase(
            snapshotName,
            lookupKind => lookupKind switch
            {
                LookupKind.ByType => CreateCase($"{snapshotName}_ByType", byTypeSource, fixture.Reference),
                LookupKind.ByAssemblyName => CreateCase($"{snapshotName}_ByAssemblyName", byAssemblyNameSource, fixture.Reference, decoy),
                LookupKind.ByAssemblyOf => CreateCase($"{snapshotName}_ByAssemblyOf", byAssemblyOfSource, fixture.Reference, decoy),
                _ => throw new ArgumentOutOfRangeException(nameof(lookupKind)),
            });
    }

    private static GenericCase BuildGenericCase(
        AssemblyType assemblyType,
        LookupKind lookupKind,
        GenericDeclarationType declarationType,
        Arity arity,
        AccessType accessType)
    {
        return (assemblyType, lookupKind) switch
        {
            (AssemblyType.CurrentAssembly, _)
                => CreateCase(
                    $"{assemblyType}_{lookupKind}_{declarationType}_{arity}_{accessType}",
                    GenericSourceFactory.CreateCurrentAssemblySource(
                        lookupKind,
                        declarationType,
                        arity,
                        accessType)),

            (AssemblyType.ExternalAssembly, LookupKind.ByType)
                => BuildExternalAssemblyByTypeCase($"{assemblyType}_{lookupKind}_{declarationType}_{arity}_{accessType}", declarationType, arity, accessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyName)
                => BuildExternalAssemblyByAssemblyNameCase($"{assemblyType}_{lookupKind}_{declarationType}_{arity}_{accessType}", declarationType, arity, accessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyOf)
                => BuildExternalAssemblyByAssemblyOfCase($"{assemblyType}_{lookupKind}_{declarationType}_{arity}_{accessType}", declarationType, arity, accessType),

            _ => throw new ArgumentOutOfRangeException(nameof(lookupKind)),
        };
    }

    private static GenericCase BuildCoexistenceCase(
        AssemblyType assemblyType,
        LookupKind lookupKind,
        AccessType nonGenericAccessType,
        AccessType genericAccessType)
    {
        return (assemblyType, lookupKind) switch
        {
            (AssemblyType.CurrentAssembly, _)
                => CreateCase(
                    $"{assemblyType}_{lookupKind}_SharedName_{nonGenericAccessType}_{genericAccessType}",
                    GenericSourceFactory.CreateCurrentAssemblyCoexistenceSource(
                        lookupKind,
                        nonGenericAccessType,
                        genericAccessType)),

            (AssemblyType.ExternalAssembly, LookupKind.ByType)
                => BuildExternalCoexistenceByTypeCase($"{assemblyType}_{lookupKind}_SharedName_{nonGenericAccessType}_{genericAccessType}", nonGenericAccessType, genericAccessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyName)
                => BuildExternalCoexistenceByAssemblyNameCase($"{assemblyType}_{lookupKind}_SharedName_{nonGenericAccessType}_{genericAccessType}", nonGenericAccessType, genericAccessType),

            (AssemblyType.ExternalAssembly, LookupKind.ByAssemblyOf)
                => BuildExternalCoexistenceByAssemblyOfCase($"{assemblyType}_{lookupKind}_SharedName_{nonGenericAccessType}_{genericAccessType}", nonGenericAccessType, genericAccessType),

            _ => throw new ArgumentOutOfRangeException(nameof(lookupKind)),
        };
    }

    private static GenericCase BuildExternalAssemblyByTypeCase(
        string snapshotName,
        GenericDeclarationType declarationType,
        Arity arity,
        AccessType accessType)
    {
        var fixtureName = $"{declarationType}{accessType}{arity}";

        var fixture = GenericAssemblyFactory.CreateExternalFixture(
            assemblyName: $"Generic.External.ByType.{declarationType}.{arity}.{accessType}",
            declarationType,
            accessType,
            arity,
            typeName: fixtureName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof(typeof(GenericFixtures.{{fixtureName}}{{GenericArityUtilities.GetOpenGenericTypeArguments(arity)}}))]
            """;

        return CreateCase(snapshotName, source, fixture.Reference);
    }

    private static GenericCase BuildExternalAssemblyByAssemblyNameCase(
        string snapshotName,
        GenericDeclarationType declarationType,
        Arity arity,
        AccessType accessType)
    {
        var fixtureName = $"{declarationType}{accessType}{arity}";
        var assemblyName = $"Generic.External.ByAssemblyName.{declarationType}.{arity}.{accessType}";

        var fixture = GenericAssemblyFactory.CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            arity,
            typeName: fixtureName);

        var decoy = GenericAssemblyFactory.CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            accessType,
            arity,
            typeName: fixtureName);

        var metadataName = GenericArityUtilities.GetMetadataName($"GenericFixtures.{fixtureName}", arity);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("{{metadataName}}", assemblyName: "{{assemblyName}}")]
            """;

        return CreateCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static GenericCase BuildExternalAssemblyByAssemblyOfCase(
        string snapshotName,
        GenericDeclarationType declarationType,
        Arity arity,
        AccessType accessType)
    {
        var fixtureName = $"{declarationType}{accessType}{arity}";
        var anchorName = $"{fixtureName}Anchor";
        var assemblyName = $"Generic.External.ByAssemblyOf.{declarationType}.{arity}.{accessType}";

        var fixture = GenericAssemblyFactory.CreateExternalFixture(
            assemblyName,
            declarationType,
            accessType,
            arity,
            typeName: fixtureName,
            includeAnchor: true,
            anchorTypeName: anchorName);

        var decoy = GenericAssemblyFactory.CreateDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            declarationType,
            accessType,
            arity,
            typeName: fixtureName);

        var metadataName = GenericArityUtilities.GetMetadataName($"GenericFixtures.{fixtureName}", arity);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("{{metadataName}}", assemblyOf: typeof(GenericFixtures.{{anchorName}}))]
            """;

        return CreateCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static GenericCase BuildExternalCoexistenceByTypeCase(
        string snapshotName,
        AccessType nonGenericAccessType,
        AccessType genericAccessType)
    {
        const string typeName = "SharedName";

        var fixture = GenericAssemblyFactory.CreateCoexistenceFixture(
            assemblyName: $"Generic.External.Coexistence.ByType.{nonGenericAccessType}.{genericAccessType}",
            nonGenericAccessType,
            genericAccessType,
            typeName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof(typeof(GenericFixtures.SharedName<>))]
            """;

        return CreateCase(snapshotName, source, fixture.Reference);
    }

    private static GenericCase BuildExternalCoexistenceByAssemblyNameCase(
        string snapshotName,
        AccessType nonGenericAccessType,
        AccessType genericAccessType)
    {
        const string typeName = "SharedName";
        var assemblyName = $"Generic.External.Coexistence.ByAssemblyName.{nonGenericAccessType}.{genericAccessType}";

        var fixture = GenericAssemblyFactory.CreateCoexistenceFixture(
            assemblyName,
            nonGenericAccessType,
            genericAccessType,
            typeName);

        var decoy = GenericAssemblyFactory.CreateCoexistenceDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            nonGenericAccessType,
            genericAccessType,
            typeName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("GenericFixtures.SharedName`1", assemblyName: "{{assemblyName}}")]
            """;

        return CreateCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static GenericCase BuildExternalCoexistenceByAssemblyOfCase(
        string snapshotName,
        AccessType nonGenericAccessType,
        AccessType genericAccessType)
    {
        const string typeName = "SharedName";
        const string anchorName = "SharedNameAnchor";
        var assemblyName = $"Generic.External.Coexistence.ByAssemblyOf.{nonGenericAccessType}.{genericAccessType}";

        var fixture = GenericAssemblyFactory.CreateCoexistenceFixture(
            assemblyName,
            nonGenericAccessType,
            genericAccessType,
            typeName,
            includeAnchor: true,
            anchorTypeName: anchorName);

        var decoy = GenericAssemblyFactory.CreateCoexistenceDecoyFixture(
            assemblyName: $"{assemblyName}.Decoy",
            nonGenericAccessType,
            genericAccessType,
            typeName);

        var source =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("GenericFixtures.SharedName`1", assemblyOf: typeof(GenericFixtures.{{anchorName}}))]
            """;

        return CreateCase(snapshotName, source, fixture.Reference, decoy);
    }

    private static GenericScenarioCase CreateScenarioCase(
        string snapshotName,
        Func<LookupKind, GenericCase> lookupCaseFactory)
    {
        return new GenericScenarioCase
        {
            SnapshotName = snapshotName,
            ByType = lookupCaseFactory(LookupKind.ByType),
            ByAssemblyName = lookupCaseFactory(LookupKind.ByAssemblyName),
            ByAssemblyOf = lookupCaseFactory(LookupKind.ByAssemblyOf),
        };
    }

    private static GenericCase CreateCase(
        string snapshotName,
        string source,
        params MetadataReference[] references)
    {
        return new GenericCase
        {
            SnapshotName = snapshotName,
            Source = source,
            References = references,
        };
    }
}
