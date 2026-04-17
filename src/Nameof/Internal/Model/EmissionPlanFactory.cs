using Nameof.Internal.Support;

namespace Nameof.Internal.Model;

internal static class EmissionPlanFactory
{
    public static EmissionPlan Create(ResolvedTypeShape shape)
    {
        var typeIdentity = TypeNameUtilities.GetTypeIdentity(shape.Identity.WrapperIdentitySource);
        var wrapperClassName = shape.IsOpenGenericDefinition
            ? $"NameofGeneric_{typeIdentity}"
            : $"Nameof_{typeIdentity}";
        var wrapperHintIdentity = shape.IsOpenGenericDefinition
            ? $"Generic.{typeIdentity}"
            : typeIdentity;

        return new EmissionPlan
        {
            NamespaceName = shape.Identity.NamespaceName,
            WrapperClassName = wrapperClassName,
            WrapperHintIdentity = wrapperHintIdentity,
            ExtensionTargetFullyQualifiedTypeName = shape.ExtensionTarget.FullyQualifiedTypeName,
            MemberNames = shape.Members.Names,
            IsOpenGenericDefinition = shape.IsOpenGenericDefinition,
            GenericArity = shape.GenericArity,
            Stub = shape.Stub,
        };
    }
}
