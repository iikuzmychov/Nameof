using Microsoft.CodeAnalysis;

namespace Nameof.Tests.Categories.Behavior.NonGeneric.Model;

internal sealed record NonGenericCase
{
    public required string SnapshotName { get; init; }
    public required string Source { get; init; }
    public required MetadataReference[] References { get; init; }
}
