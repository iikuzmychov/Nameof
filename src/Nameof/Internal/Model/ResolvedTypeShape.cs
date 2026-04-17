namespace Nameof.Internal.Model;

internal sealed record ResolvedTypeShape
{
    public required ResolvedTargetIdentity Identity { get; init; }
    public required ExtensionTarget ExtensionTarget { get; init; }
    public required ResolvedMembers Members { get; init; }
    public StubPlan? Stub { get; init; }
    public bool IsOpenGenericDefinition { get; init; }
    public int GenericArity { get; init; }
}
