using WildDotNet.Nameof.Tests.TestInfrastructure;

namespace WildDotNet.Nameof.Tests;

public class NameofGeneratorBclRegressionTests
{
    [Fact]
    public Task Generates_nameof_for_bcl_public_type_using_typeof()
    {
        const string source =
            """
            using System;
            using WildDotNet.Nameof;

            [assembly: GenerateNameof(typeof(ConsoleKeyInfo))]
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }

    [Fact]
    public Task Generates_nameof_for_bcl_internal_type_using_assembly_of()
    {
        const string source =
            """
            using System;
            using WildDotNet.Nameof;

            [assembly: GenerateNameof("System.IO.ConsoleStream", assemblyOf: typeof(Console))]
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result.ToSnapshot());
    }
}
