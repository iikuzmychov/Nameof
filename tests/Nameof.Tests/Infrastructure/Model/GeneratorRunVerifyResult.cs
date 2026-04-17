using System.Collections.Immutable;

namespace Nameof.Tests.Infrastructure.Model;

internal sealed record GeneratorRunVerifyResult
{
    public ImmutableArray<DiagnosticVerifyResult>? Diagnostics { get; init; }
    public ImmutableArray<GeneratedSourceVerifyResult>? GeneratedSources { get; init; }
}
