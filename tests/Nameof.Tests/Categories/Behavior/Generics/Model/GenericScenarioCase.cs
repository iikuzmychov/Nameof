namespace Nameof.Tests.Categories.Behavior.Generics.Model;

internal sealed record GenericScenarioCase
{
    public required string SnapshotName { get; init; }
    public required GenericCase ByType { get; init; }
    public required GenericCase ByAssemblyName { get; init; }
    public required GenericCase ByAssemblyOf { get; init; }
}
