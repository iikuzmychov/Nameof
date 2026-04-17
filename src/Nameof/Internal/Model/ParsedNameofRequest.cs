using Microsoft.CodeAnalysis;

namespace Nameof.Internal.Model;

internal sealed record ParsedNameofRequest
{
    public required RequestTarget Target { get; init; }
    public required RequestGenericInfo Generic { get; init; }
    public Location? AttributeLocation { get; init; }
}
