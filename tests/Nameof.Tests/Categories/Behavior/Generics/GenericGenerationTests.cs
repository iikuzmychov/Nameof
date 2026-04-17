using Nameof.Tests.Categories.Behavior.Generics.Factories;
using Nameof.Tests.Categories.Behavior.Generics.Model;
using Nameof.Tests.Infrastructure;
using Nameof.Tests.Infrastructure.Model;

namespace Nameof.Tests.Categories.Behavior.Generics;

public class GenericGenerationTests
{
    [Theory]
    [CombinatorialData]
    public Task Generates_nameof_generic_behavior(
        AssemblyType assemblyType,
        GenericDeclarationType declarationType,
        Arity arity,
        AccessType accessType)
    {
        var testCase = GenericScenarioFactory.BuildGenericScenarioCase(
            assemblyType,
            declarationType,
            arity,
            accessType);

        var byType = GeneratorTestDriver.Run(testCase.ByType.Source, testCase.ByType.References);
        var byAssemblyName = GeneratorTestDriver.Run(testCase.ByAssemblyName.Source, testCase.ByAssemblyName.References);
        var byAssemblyOf = GeneratorTestDriver.Run(testCase.ByAssemblyOf.Source, testCase.ByAssemblyOf.References);

        Assert.Equivalent(byType, byAssemblyName);
        Assert.Equivalent(byType, byAssemblyOf);

        return Verify(byType).UseTextForParameters(testCase.SnapshotName);
    }

    [Theory]
    [CombinatorialData]
    public Task Generates_nameof_generic_coexistence(
        AssemblyType assemblyType,
        AccessType nonGenericAccessType,
        AccessType genericAccessType)
    {
        var testCase = GenericScenarioFactory.BuildCoexistenceScenarioCase(
            assemblyType,
            nonGenericAccessType,
            genericAccessType);

        var byType = GeneratorTestDriver.Run(testCase.ByType.Source, testCase.ByType.References);
        var byAssemblyName = GeneratorTestDriver.Run(testCase.ByAssemblyName.Source, testCase.ByAssemblyName.References);
        var byAssemblyOf = GeneratorTestDriver.Run(testCase.ByAssemblyOf.Source, testCase.ByAssemblyOf.References);

        Assert.Equivalent(byType, byAssemblyName);
        Assert.Equivalent(byType, byAssemblyOf);

        return Verify(byType).UseTextForParameters(testCase.SnapshotName);
    }
}
