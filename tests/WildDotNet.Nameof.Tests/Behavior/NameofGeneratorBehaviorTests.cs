using WildDotNet.Nameof.Tests.Behavior.Factories;
using WildDotNet.Nameof.Tests.Behavior.Model;
using WildDotNet.Nameof.Tests.TestInfrastructure;

namespace WildDotNet.Nameof.Tests.Behavior;

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
        var testCase = NameofGeneratorBehaviorTestCaseFactory.BuildBehaviorCase(
            assemblyType,
            lookupKind,
            declarationType,
            accessType);

        var result = GeneratorTestDriver.Run(testCase.Source, testCase.References);

        return Verify(result.ToSnapshot()).UseTextForParameters(testCase.SnapshotName);
    }
}
