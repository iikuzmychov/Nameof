namespace Nameof.Internal.Model;

internal sealed record ExtensionTarget
{
    public required string FullyQualifiedTypeName { get; init; }
}
