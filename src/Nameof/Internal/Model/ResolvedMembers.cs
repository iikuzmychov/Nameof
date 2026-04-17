using System.Collections.Generic;

namespace Nameof.Internal.Model;

internal sealed record ResolvedMembers(
    IReadOnlyCollection<string> Names);
