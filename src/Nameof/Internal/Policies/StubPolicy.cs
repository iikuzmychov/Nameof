using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Nameof.Internal.Policies;

internal static class StubPolicy
{
    private static readonly StubKind EnumStubKind = new()
    {
        TypeKeyword = "enum",
        SealedKeyword = string.Empty,
        NeedsPrivateConstructor = false,
    };

    private static readonly StubKind InterfaceStubKind = new()
    {
        TypeKeyword = "interface",
        SealedKeyword = string.Empty,
        NeedsPrivateConstructor = false,
    };

    private static readonly StubKind StructStubKind = new()
    {
        TypeKeyword = "struct",
        SealedKeyword = string.Empty,
        NeedsPrivateConstructor = false,
    };

    private static readonly StubKind ClassStubKind = new()
    {
        TypeKeyword = "class",
        SealedKeyword = " sealed",
        NeedsPrivateConstructor = true,
    };

    internal static bool NeedsStub(INamedTypeSymbol type)
    {
        return type.DeclaredAccessibility != Accessibility.Public &&
               !type.Locations.Any(static location => location.IsInSource);
    }

    internal static StubKind GetStubKind(INamedTypeSymbol type)
    {
        return type.TypeKind switch
        {
            TypeKind.Enum => EnumStubKind,
            TypeKind.Interface => InterfaceStubKind,
            TypeKind.Struct => StructStubKind,
            _ => ClassStubKind
        };
    }

    internal static StubKind GetStubKind(Type type)
    {
        if (type.IsEnum)
        {
            return EnumStubKind;
        }

        if (type.IsInterface)
        {
            return InterfaceStubKind;
        }

        if (type.IsValueType)
        {
            return StructStubKind;
        }

        return ClassStubKind;
    }
}
