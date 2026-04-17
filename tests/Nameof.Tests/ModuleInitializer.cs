using DiffEngine;
using System.Runtime.CompilerServices;

namespace Nameof.Tests;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        DiffRunner.Disabled = true;
    }
}
