using Nameof.Tests.Infrastructure;

namespace Nameof.Tests.Categories.Diagnostics;

public class StubUsageAnalyzerTests
{
    [Fact]
    public Task Reports_error_for_stub_type_used_as_field_type()
    {
        const string source =
            """
            using System;
            using System.IO;
            using Nameof;

            [assembly: GenerateNameof("System.IO.ConsoleStream", assemblyOf: typeof(Console))]

            namespace TestNamespace;

            internal sealed class Sample
            {
                private ConsoleStream _stream;
            }
            """;

        var result = GeneratorTestDriver.RunWithAnalyzer(source);

        return Verify(result);
    }

    [Fact]
    public Task Reports_error_for_stub_type_used_in_typeof()
    {
        const string source =
            """
            using System;
            using System.IO;
            using Nameof;

            [assembly: GenerateNameof("System.IO.ConsoleStream", assemblyOf: typeof(Console))]

            namespace TestNamespace;

            internal static class Sample
            {
                private static readonly Type Value = typeof(ConsoleStream);
            }
            """;

        var result = GeneratorTestDriver.RunWithAnalyzer(source);

        return Verify(result);
    }

    [Fact]
    public void Does_not_report_error_for_stub_type_used_in_nameof()
    {
        const string source =
            """
            using System;
            using System.IO;
            using Nameof;

            [assembly: GenerateNameof("System.IO.ConsoleStream", assemblyOf: typeof(Console))]

            namespace TestNamespace;

            internal static class Sample
            {
                private static readonly string Value = nameof<ConsoleStream>._canRead + nameof(ConsoleStream);
            }
            """;

        var result = GeneratorTestDriver.RunWithAnalyzer(source);

        Assert.Null(result.Diagnostics);
    }
}
