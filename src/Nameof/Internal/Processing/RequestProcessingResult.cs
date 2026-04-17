using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Nameof.Internal.Model;

namespace Nameof.Internal.Processing;

internal sealed record RequestProcessingResult
{
    public EmissionPlan? EmissionPlan { get; init; }
    public required ImmutableArray<Diagnostic> Diagnostics { get; init; }
}
