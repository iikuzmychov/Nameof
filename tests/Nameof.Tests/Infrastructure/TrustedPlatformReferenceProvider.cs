using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Nameof.Tests.Infrastructure;

internal static class TrustedPlatformReferenceProvider
{
    public static ImmutableArray<MetadataReference> GetDefaultReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        Assert.NotNull(trustedPlatformAssemblies);

        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);

            if (!seenPaths.Add(fullPath))
            {
                continue;
            }

            builder.Add(MetadataReference.CreateFromFile(fullPath));
        }

        return builder.ToImmutable();
    }
}
