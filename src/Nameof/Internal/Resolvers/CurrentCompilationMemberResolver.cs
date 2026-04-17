using System;
using Microsoft.CodeAnalysis;
using Nameof.Internal.Model;
using Nameof.Internal.Policies;

namespace Nameof.Internal.Resolvers;

internal sealed class CurrentCompilationMemberResolver : ITypeMemberResolver
{
    public bool CanResolve(ParsedNameofRequest request, Compilation compilation)
    {
        if (request.Target.Symbol is INamedTypeSymbol symbol)
        {
            return SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilation.Assembly);
        }

        return string.Equals(request.Target.RequestedAssemblyName, compilation.Assembly.Identity.Name, StringComparison.Ordinal);
    }

    public ResolvedTypeShape? Resolve(ParsedNameofRequest request, Compilation compilation)
    {
        var type = request.Target.Symbol ?? compilation.GetTypeByMetadataName(request.Target.FullTypeName!);
        if (type is null)
        {
            return null;
        }

        var memberNames = MemberInclusionPolicy.FilterSymbolMembers(type, compilation);
        return ResolvedTypeShapeFactory.CreateResolvedSymbolType(
            type,
            memberNames,
            request.Generic.IsOpenDefinition);
    }
}
