using Microsoft.CodeAnalysis;

namespace Nameof.Internal.Model;

internal readonly record struct ParsedNameofRequest(
    RequestTarget Target,
    RequestGenericInfo Generic,
    Location? AttributeLocation);
