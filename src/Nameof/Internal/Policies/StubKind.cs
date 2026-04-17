namespace Nameof.Internal.Policies;

internal sealed record StubKind
{
    public required string TypeKeyword { get; init; }
    public required string SealedKeyword { get; init; }
    public required bool NeedsPrivateConstructor { get; init; }
}
