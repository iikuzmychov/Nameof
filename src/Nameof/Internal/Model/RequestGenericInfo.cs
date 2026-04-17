using System;

namespace Nameof.Internal.Model;

internal sealed record RequestGenericInfo
{
    private enum Kind
    {
        NonGeneric,
        OpenDefinition,
        ClosedGeneric
    }

    private Kind GenericKind { get; init; }

    public int Arity { get; init; }

    public bool IsOpenDefinition => GenericKind == Kind.OpenDefinition;

    public bool IsClosedGeneric => GenericKind == Kind.ClosedGeneric;

    public static RequestGenericInfo NonGeneric()
        => Create(Kind.NonGeneric, 0);

    public static RequestGenericInfo OpenDefinition(int arity)
        => Create(Kind.OpenDefinition, arity);

    public static RequestGenericInfo ClosedGeneric()
        => Create(Kind.ClosedGeneric, 0);

    private static RequestGenericInfo Create(Kind kind, int arity)
    {
        if (kind == Kind.OpenDefinition && arity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arity), "Open generic definitions require a positive arity.");
        }

        if (kind != Kind.OpenDefinition && arity != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arity), "Only open generic definitions can carry arity.");
        }

        return new RequestGenericInfo
        {
            GenericKind = kind,
            Arity = arity,
        };
    }
}
