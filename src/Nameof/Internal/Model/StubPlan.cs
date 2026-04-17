using Nameof.Internal.Policies;

namespace Nameof.Internal.Model;

internal sealed record StubPlan
{
    public required string Identity { get; init; }
    public string? NamespaceName { get; init; }
    public required string TypeName { get; init; }
    public string? TypeParameters { get; init; }
    public required StubKind Kind { get; init; }
}
