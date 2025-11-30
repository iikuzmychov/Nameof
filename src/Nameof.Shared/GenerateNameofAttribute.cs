namespace Nameof.Shared;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly)]
public sealed class GenerateNameofAttribute : Attribute
{
    public NameofAccessModifier AccessModifier { get; }

    public GenerateNameofAttribute()
    {
        AccessModifier = NameofAccessModifier.Public;
    }

    public GenerateNameofAttribute(NameofAccessModifier accessModifier)
    {
        AccessModifier = accessModifier;
    }
}
