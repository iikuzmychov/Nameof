using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nameof.Tests.Infrastructure;

internal static class InMemoryAssemblyReferenceFactory
{
    public static MetadataReference Create(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            TrustedPlatformReferenceProvider.GetDefaultReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));

        var image = peStream.ToArray();
        Assembly.Load(image);

        return MetadataReference.CreateFromImage(image);
    }
}
