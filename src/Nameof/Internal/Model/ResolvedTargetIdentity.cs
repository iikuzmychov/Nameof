namespace Nameof.Internal.Model;

internal sealed record ResolvedTargetIdentity
{
    public required string WrapperIdentitySource { get; init; }
    public string? NamespaceName { get; init; }
    public required string TypeName { get; init; }
}
