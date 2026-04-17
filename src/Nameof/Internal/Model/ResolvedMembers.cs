using System.Collections.Generic;

namespace Nameof.Internal.Model;

internal sealed record ResolvedMembers
{
    public required IReadOnlyCollection<string> Names { get; init; }
}
