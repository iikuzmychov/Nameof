using Microsoft.CodeAnalysis;

namespace Nameof.Tests.Categories.Behavior.Generics.Model;

internal sealed record GenericCase
{
    public required string SnapshotName { get; init; }
    public required string Source { get; init; }
    public required MetadataReference[] References { get; init; }
}
