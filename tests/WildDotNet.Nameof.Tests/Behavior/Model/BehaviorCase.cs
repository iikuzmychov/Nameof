using Microsoft.CodeAnalysis;

namespace WildDotNet.Nameof.Tests.Behavior.Model;

internal sealed record BehaviorCase(
    string SnapshotName,
    string Source,
    MetadataReference[] References);
