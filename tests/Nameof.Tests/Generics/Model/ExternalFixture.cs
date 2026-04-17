using Microsoft.CodeAnalysis;

namespace Nameof.Tests.Generics.Model;

internal sealed record ExternalFixture(
    MetadataReference Reference,
    string TypeName,
    string AssemblyName);
