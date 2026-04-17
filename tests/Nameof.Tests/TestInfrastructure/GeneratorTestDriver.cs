using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nameof.Tests.TestInfrastructure;

internal static class GeneratorTestDriver
{
    public static GeneratorRunResult Run(string source, params MetadataReference[] extraReferences)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: GetDefaultReferences().AddRange(extraReferences),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new NameofGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results.Length == 0
            ? ImmutableArray<GeneratedSourceResult>.Empty
            : runResult.Results[0].GeneratedSources
                .Select(static source => new GeneratedSourceResult(
                    source.HintName,
                    NormalizeLineEndings(source.SourceText.ToString())))
                .ToImmutableArray();

        using var emitStream = new MemoryStream();
        var emitResult = outputCompilation.Emit(emitStream);
        Assert.True(
            emitResult.Success,
            string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));

        return new GeneratorRunResult(
            diagnostics.AddRange(outputCompilation.GetDiagnostics()).AddRange(emitResult.Diagnostics),
            generatedSources);
    }

    private static ImmutableArray<MetadataReference> GetDefaultReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                AddReference(path);
            }
        }

        AddReference(Path.Combine(AppContext.BaseDirectory, "Nameof.dll"));

        return builder.ToImmutable();

        void AddReference(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!File.Exists(path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (!seenPaths.Add(fullPath))
            {
                return;
            }

            builder.Add(MetadataReference.CreateFromFile(fullPath));
        }
    }

    internal static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}

internal sealed record GeneratorRunResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources)
{
    public GeneratorRunSnapshot ToSnapshot()
    {
        var diagnostics = Diagnostics
            .Where(static diagnostic => diagnostic.Id.StartsWith("NAMEOF", StringComparison.Ordinal))
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.GetMessage(), StringComparer.Ordinal)
            .Select(static diagnostic => new DiagnosticSnapshot(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                GeneratorTestDriver.NormalizeLineEndings(diagnostic.GetMessage()),
                GetDiagnosticLocation(diagnostic)))
            .ToImmutableArray();

        var generatedSources = GeneratedSources
            .OrderBy(static generatedSource => generatedSource.HintName, StringComparer.Ordinal)
            .Select(static generatedSource => new GeneratedSourceSnapshot(
                generatedSource.HintName,
                generatedSource.Source))
            .ToImmutableArray();

        return new GeneratorRunSnapshot(diagnostics, generatedSources);
    }

    private static string? GetDiagnosticLocation(Diagnostic diagnostic)
    {
        if (diagnostic.Location is null || !diagnostic.Location.IsInSource)
        {
            return null;
        }

        var lineSpan = diagnostic.Location.GetLineSpan();
        return $"{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}";
    }
}

internal sealed record GeneratedSourceResult(
    string HintName,
    string Source);

internal sealed record GeneratorRunSnapshot(
    ImmutableArray<DiagnosticSnapshot> Diagnostics,
    ImmutableArray<GeneratedSourceSnapshot> GeneratedSources);

internal sealed record DiagnosticSnapshot(
    string Id,
    string Severity,
    string Message,
    string? Location);

internal sealed record GeneratedSourceSnapshot(
    string HintName,
    string Source);
