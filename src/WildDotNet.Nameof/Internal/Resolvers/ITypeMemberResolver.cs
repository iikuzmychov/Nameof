using Microsoft.CodeAnalysis;
using WildDotNet.Nameof.Internal.Model;

namespace WildDotNet.Nameof.Internal.Resolvers;

internal interface ITypeMemberResolver
{
    bool CanResolve(NameofRequest request, Compilation compilation);

    ResolvedNameofType? Resolve(NameofRequest request, Compilation compilation);
}
