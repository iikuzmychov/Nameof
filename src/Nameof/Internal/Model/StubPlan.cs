using Nameof.Internal.Policies;

namespace Nameof.Internal.Model;

internal sealed record StubPlan(
    string Identity,
    string? NamespaceName,
    string TypeName,
    string? TypeParameters,
    StubKind Kind);
