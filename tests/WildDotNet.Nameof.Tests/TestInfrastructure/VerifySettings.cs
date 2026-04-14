using System.Runtime.CompilerServices;
using DiffEngine;

namespace WildDotNet.Nameof.Tests.TestInfrastructure;

internal static class VerifySettings
{
    [ModuleInitializer]
    public static void Initialize()
    {
        DiffRunner.Disabled = true;
    }
}
