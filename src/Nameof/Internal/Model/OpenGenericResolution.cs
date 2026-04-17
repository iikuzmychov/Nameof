namespace Nameof.Internal.Model;

internal sealed record OpenGenericResolution
{
    public required ExtensionTarget ExtensionTarget { get; init; }
    public StubPlan? Stub { get; init; }
}
