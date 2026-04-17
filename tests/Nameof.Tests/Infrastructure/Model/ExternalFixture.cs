using Microsoft.CodeAnalysis;

namespace Nameof.Tests.Infrastructure.Model;

internal sealed record ExternalFixture
{
    public required MetadataReference Reference { get; init; }
    public required string TypeName { get; init; }
    public required string AssemblyName { get; init; }
}
