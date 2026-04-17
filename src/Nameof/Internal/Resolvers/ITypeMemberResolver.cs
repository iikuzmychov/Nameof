using Microsoft.CodeAnalysis;
using Nameof.Internal.Model;

namespace Nameof.Internal.Resolvers;

internal interface ITypeMemberResolver
{
    bool CanResolve(ParsedNameofRequest request, Compilation compilation);

    ResolvedTypeShape? Resolve(ParsedNameofRequest request, Compilation compilation);
}
