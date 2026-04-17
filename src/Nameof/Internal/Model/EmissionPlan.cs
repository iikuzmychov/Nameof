using System.Collections.Generic;

namespace Nameof.Internal.Model;

internal sealed record EmissionPlan
{
    public string? NamespaceName { get; init; }
    public required string WrapperClassName { get; init; }
    public required string WrapperHintIdentity { get; init; }
    public required string ExtensionTargetFullyQualifiedTypeName { get; init; }
    public required IReadOnlyCollection<string> MemberNames { get; init; }
    public bool IsOpenGenericDefinition { get; init; }
    public int GenericArity { get; init; }
    public StubPlan? Stub { get; init; }
}
