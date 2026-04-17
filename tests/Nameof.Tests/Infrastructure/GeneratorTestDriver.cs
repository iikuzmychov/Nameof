using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Nameof.Tests.Infrastructure.Model;

namespace Nameof.Tests.Infrastructure;

internal static class GeneratorTestDriver
{
    public static GeneratorRunVerifyResult Run(string source, params MetadataReference[] extraReferences)
        => Run(source, runAnalyzer: false, extraReferences);

    public static GeneratorRunVerifyResult RunWithAnalyzer(string source, params MetadataReference[] extraReferences)
        => Run(source, runAnalyzer: true, extraReferences);

    private static GeneratorRunVerifyResult Run(
        string source,
        bool runAnalyzer,
        params MetadataReference[] extraReferences)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: TrustedPlatformReferenceProvider.GetDefaultReferences().AddRange(extraReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new NameofGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        using var emitStream = new MemoryStream();
        var emitResult = outputCompilation.Emit(emitStream);
        var analyzerDiagnostics = runAnalyzer
            ? outputCompilation
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new NameofStubUsageAnalyzer()))
                .GetAnalyzerDiagnosticsAsync()
                .GetAwaiter()
                .GetResult()
            : [];

        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));

        var allDiagnostics = diagnostics
            .AddRange(emitResult.Diagnostics)
            .AddRange(analyzerDiagnostics);

        var nameofDiagnostics = allDiagnostics
            .Where(static diagnostic => diagnostic.Id.StartsWith("NAMEOF", StringComparison.Ordinal))
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.GetMessage(), StringComparer.Ordinal)
            .Select(static diagnostic => new DiagnosticVerifyResult
            {
                Id = diagnostic.Id,
                Severity = diagnostic.Severity.ToString(),
                Message = diagnostic.GetMessage(),
                Location = GetLocation(diagnostic),
            })
            .ToImmutableArray();

        var generatedSources = driver
            .GetRunResult()
            .Results[0]
            .GeneratedSources
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .Select(static source => new GeneratedSourceVerifyResult
            {
                HintName = source.HintName,
                Source = source.SourceText.ToString(),
            })
            .ToImmutableArray();

        return new GeneratorRunVerifyResult
        {
            Diagnostics = nameofDiagnostics.Length == 0 ? null : nameofDiagnostics,
            GeneratedSources = generatedSources.Length == 0 ? null : generatedSources,
        };
    }

    private static string? GetLocation(Diagnostic diagnostic)
    {
        if (!diagnostic.Location.IsInSource)
        {
            return null;
        }

        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}";
    }
}
