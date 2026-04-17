using Nameof.Tests.Infrastructure;

namespace Nameof.Tests.Categories.Regression;

public class BclRegressionTests
{
    [Fact]
    public Task Generates_nameof_for_bcl_public_type_using_typeof()
    {
        const string source =
            """
            using System;
            using Nameof;

            [assembly: GenerateNameof(typeof(ConsoleKeyInfo))]
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result);
    }

    [Fact]
    public Task Generates_nameof_for_bcl_internal_type_using_assembly_of()
    {
        const string source =
            """
            using System;
            using Nameof;

            [assembly: GenerateNameof("System.IO.ConsoleStream", assemblyOf: typeof(Console))]
            """;

        var result = GeneratorTestDriver.Run(source);

        return Verify(result);
    }

    [Fact]
    public Task Generates_nameof_for_bcl_open_generic_list_using_lookup_kind_equivalence()
    {
        return RunOpenGenericRegressionScenario(
            nameof(Generates_nameof_for_bcl_open_generic_list_using_lookup_kind_equivalence),
            "typeof(List<>)",
            "System.Collections.Generic.List`1");
    }

    [Fact]
    public Task Generates_nameof_for_bcl_open_generic_dictionary_using_lookup_kind_equivalence()
    {
        return RunOpenGenericRegressionScenario(
            nameof(Generates_nameof_for_bcl_open_generic_dictionary_using_lookup_kind_equivalence),
            "typeof(Dictionary<,>)",
            "System.Collections.Generic.Dictionary`2");
    }

    private static Task RunOpenGenericRegressionScenario(string snapshotName, string typeExpression, string metadataName)
    {
        var assemblyName = typeof(List<>).Assembly.GetName().Name!;

        var byTypeSource =
            $$"""
            using System.Collections.Generic;
            using Nameof;

            [assembly: GenerateNameof({{typeExpression}})]
            """;

        var byAssemblyNameSource =
            $$"""
            using Nameof;

            [assembly: GenerateNameof("{{metadataName}}", assemblyName: "{{assemblyName}}")]
            """;

        var byAssemblyOfSource =
            $$"""
            using System.Collections.Generic;
            using Nameof;

            [assembly: GenerateNameof("{{metadataName}}", assemblyOf: typeof(Dictionary<,>))]
            """;

        var byType = GeneratorTestDriver.Run(byTypeSource);
        var byAssemblyName = GeneratorTestDriver.Run(byAssemblyNameSource);
        var byAssemblyOf = GeneratorTestDriver.Run(byAssemblyOfSource);

        Assert.Equivalent(byType, byAssemblyName);
        Assert.Equivalent(byType, byAssemblyOf);

        return Verify(byType).UseTextForParameters(snapshotName);
    }
}
