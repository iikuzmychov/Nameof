namespace Nameof.Tests.Categories.Behavior.NonGeneric.Model;

internal sealed record NonGenericScenarioCase
{
    public required string SnapshotName { get; init; }
    public required NonGenericCase ByType { get; init; }
    public required NonGenericCase ByAssemblyName { get; init; }
    public required NonGenericCase ByAssemblyOf { get; init; }
}
