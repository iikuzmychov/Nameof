using Microsoft.CodeAnalysis;

namespace Nameof.Tests.Generics.Model;

internal sealed record GenericBehaviorCase(
    string SnapshotName,
    string Source,
    MetadataReference[] References);

internal sealed record GenericBehaviorScenarioCase(
    string SnapshotName,
    GenericBehaviorCase ByType,
    GenericBehaviorCase ByAssemblyName,
    GenericBehaviorCase ByAssemblyOf);
