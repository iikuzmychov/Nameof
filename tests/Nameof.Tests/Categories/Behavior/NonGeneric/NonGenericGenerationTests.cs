using Nameof.Tests.Categories.Behavior.NonGeneric.Factories;
using Nameof.Tests.Categories.Behavior.NonGeneric.Model;
using Nameof.Tests.Infrastructure;
using Nameof.Tests.Infrastructure.Model;

namespace Nameof.Tests.Categories.Behavior.NonGeneric;

public class NonGenericGenerationTests
{
    [Theory]
    [CombinatorialData]
    public Task Generates_nameof_non_generic(
        AssemblyType assemblyType,
        DeclarationType declarationType,
        AccessType accessType)
    {
        var testCase = NonGenericScenarioFactory.BuildScenarioCase(
            assemblyType,
            declarationType,
            accessType);

        var byType = GeneratorTestDriver.Run(testCase.ByType.Source, testCase.ByType.References);
        var byAssemblyName = GeneratorTestDriver.Run(testCase.ByAssemblyName.Source, testCase.ByAssemblyName.References);
        var byAssemblyOf = GeneratorTestDriver.Run(testCase.ByAssemblyOf.Source, testCase.ByAssemblyOf.References);

        Assert.Equivalent(byType, byAssemblyName);
        Assert.Equivalent(byType, byAssemblyOf);

        return Verify(byType).UseTextForParameters(testCase.SnapshotName);
    }
}
