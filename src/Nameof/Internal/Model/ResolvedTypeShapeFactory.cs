using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Nameof.Internal.Policies;
using Nameof.Internal.Support;

namespace Nameof.Internal.Model;

internal static class ResolvedTypeShapeFactory
{
    public static ResolvedTypeShape? CreateResolvedSymbolType(
        INamedTypeSymbol type,
        HashSet<string> memberNames,
        bool isOpenGenericDefinition)
    {
        if (memberNames.Count == 0)
        {
            return null;
        }

        return isOpenGenericDefinition
            ? CreateOpenGenericSymbolShape(type, memberNames)
            : CreateNonGenericSymbolShape(type, memberNames);
    }

    public static ResolvedTypeShape? CreateResolvedRuntimeType(
        Compilation compilation,
        Type type,
        HashSet<string> memberNames,
        bool isOpenGenericDefinition)
    {
        if (memberNames.Count == 0)
        {
            return null;
        }

        return isOpenGenericDefinition
            ? CreateOpenGenericRuntimeShape(compilation, type, memberNames)
            : CreateNonGenericRuntimeShape(compilation, type, memberNames);
    }

    private static ResolvedTypeShape CreateNonGenericSymbolShape(
        INamedTypeSymbol type,
        HashSet<string> memberNames)
    {
        return new ResolvedTypeShape
        {
            Identity = CreateSymbolIdentity(type),
            ExtensionTarget = CreateSymbolExtensionTarget(type),
            Members = new ResolvedMembers { Names = memberNames },
            Stub = CreateNonGenericSymbolStub(type),
        };
    }

    private static ResolvedTypeShape CreateOpenGenericSymbolShape(
        INamedTypeSymbol type,
        HashSet<string> memberNames)
    {
        var metadataFullName = TypeNameUtilities.GetMetadataFullName(type);
        var rootMetadataFullName = TypeNameUtilities.GetRootMetadataFullName(metadataFullName);
        var resolution = ResolveOpenGenericSymbol(type, rootMetadataFullName);

        return new ResolvedTypeShape
        {
            Identity = CreateOpenGenericSymbolIdentity(type, metadataFullName),
            ExtensionTarget = resolution.ExtensionTarget,
            Members = new ResolvedMembers { Names = memberNames },
            Stub = resolution.Stub,
            IsOpenGenericDefinition = true,
            GenericArity = type.Arity,
        };
    }

    private static ResolvedTypeShape CreateNonGenericRuntimeShape(
        Compilation compilation,
        Type type,
        HashSet<string> memberNames)
    {
        var fullTypeName = GetRequiredFullName(type);

        return new ResolvedTypeShape
        {
            Identity = CreateRuntimeIdentity(fullTypeName),
            ExtensionTarget = CreateRuntimeExtensionTarget(fullTypeName),
            Members = new ResolvedMembers { Names = memberNames },
            Stub = CreateNonGenericRuntimeStub(compilation, type, fullTypeName),
        };
    }

    private static ResolvedTypeShape CreateOpenGenericRuntimeShape(
        Compilation compilation,
        Type type,
        HashSet<string> memberNames)
    {
        var fullTypeName = GetRequiredFullName(type);
        var rootMetadataFullName = TypeNameUtilities.GetRootMetadataFullName(fullTypeName);
        var resolution = ResolveOpenGenericRuntime(compilation, type, rootMetadataFullName);

        return new ResolvedTypeShape
        {
            Identity = CreateOpenGenericRuntimeIdentity(type, fullTypeName),
            ExtensionTarget = resolution.ExtensionTarget,
            Members = new ResolvedMembers { Names = memberNames },
            Stub = resolution.Stub,
            IsOpenGenericDefinition = true,
            GenericArity = type.GetGenericArguments().Length,
        };
    }

    private static ResolvedTargetIdentity CreateSymbolIdentity(INamedTypeSymbol type)
    {
        return new ResolvedTargetIdentity
        {
            WrapperIdentitySource = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
            NamespaceName = GetContainingNamespace(type),
            TypeName = type.Name,
        };
    }

    private static ResolvedTargetIdentity CreateOpenGenericSymbolIdentity(INamedTypeSymbol type, string metadataFullName)
    {
        return new ResolvedTargetIdentity
        {
            WrapperIdentitySource = metadataFullName,
            NamespaceName = GetContainingNamespace(type),
            TypeName = type.Name,
        };
    }

    private static ResolvedTargetIdentity CreateRuntimeIdentity(string fullTypeName)
    {
        var (namespaceName, typeName) = TypeNameUtilities.SplitNamespaceAndTypeName(fullTypeName);
        return new ResolvedTargetIdentity
        {
            WrapperIdentitySource = fullTypeName,
            NamespaceName = namespaceName,
            TypeName = typeName,
        };
    }

    private static ResolvedTargetIdentity CreateOpenGenericRuntimeIdentity(Type type, string fullTypeName)
    {
        var (namespaceName, _) = TypeNameUtilities.SplitNamespaceAndTypeName(fullTypeName);
        return new ResolvedTargetIdentity
        {
            WrapperIdentitySource = fullTypeName,
            NamespaceName = namespaceName,
            TypeName = TypeNameUtilities.GetRootTypeName(type),
        };
    }

    private static ExtensionTarget CreateSymbolExtensionTarget(INamedTypeSymbol type)
    {
        return new ExtensionTarget
        {
            FullyQualifiedTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        };
    }

    private static ExtensionTarget CreateRuntimeExtensionTarget(string fullTypeName)
    {
        return new ExtensionTarget
        {
            FullyQualifiedTypeName = $"global::{fullTypeName}",
        };
    }

    private static OpenGenericResolution ResolveOpenGenericSymbol(
        INamedTypeSymbol type,
        string rootMetadataFullName)
    {
        var accessibleSibling = FindAccessibleNonGenericSibling(type);
        return accessibleSibling is not null
            ? new OpenGenericResolution
            {
                ExtensionTarget = CreateSiblingExtensionTarget(accessibleSibling),
            }
            : new OpenGenericResolution
            {
                ExtensionTarget = CreateRootMetadataExtensionTarget(rootMetadataFullName),
                Stub = CreateStubPlan(
                    GetContainingNamespace(type),
                    type.Name,
                    null,
                    StubPolicy.GetStubKind(type)),
            };
    }

    private static ExtensionTarget CreateSiblingExtensionTarget(INamedTypeSymbol sibling)
    {
        return new ExtensionTarget
        {
            FullyQualifiedTypeName = sibling.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        };
    }

    private static ExtensionTarget CreateRootMetadataExtensionTarget(string rootMetadataFullName)
    {
        return new ExtensionTarget
        {
            FullyQualifiedTypeName = $"global::{rootMetadataFullName}",
        };
    }

    private static StubPlan? CreateNonGenericSymbolStub(INamedTypeSymbol type)
    {
        return StubPolicy.NeedsStub(type)
            ? CreateStubPlan(
                GetContainingNamespace(type),
                type.Name,
                TypeNameUtilities.FormatTypeParameters(type),
                StubPolicy.GetStubKind(type))
            : null;
    }

    private static StubPlan? CreateNonGenericRuntimeStub(
        Compilation compilation,
        Type type,
        string fullTypeName)
    {
        if (compilation.GetTypeByMetadataName(fullTypeName) is not null)
        {
            return null;
        }

        var (namespaceName, typeName) = TypeNameUtilities.SplitNamespaceAndTypeName(fullTypeName);
        return CreateStubPlan(
            namespaceName,
            typeName,
            TypeNameUtilities.FormatTypeParameters(type),
            StubPolicy.GetStubKind(type));
    }

    private static OpenGenericResolution ResolveOpenGenericRuntime(
        Compilation compilation,
        Type type,
        string rootMetadataFullName)
    {
        var assemblyName = GetAssemblyName(type);
        var accessibleSibling = FindAccessibleReferencedNonGenericSibling(compilation, assemblyName, rootMetadataFullName);

        if (accessibleSibling is not null)
        {
            return new OpenGenericResolution
            {
                ExtensionTarget = CreateSiblingExtensionTarget(accessibleSibling),
            };
        }

        var fullTypeName = GetRequiredFullName(type);
        var (namespaceName, _) = TypeNameUtilities.SplitNamespaceAndTypeName(fullTypeName);
        return new OpenGenericResolution
        {
            ExtensionTarget = CreateRootMetadataExtensionTarget(rootMetadataFullName),
            Stub = CreateStubPlan(
                namespaceName,
                TypeNameUtilities.GetRootTypeName(type),
                null,
                StubPolicy.GetStubKind(type)),
        };
    }

    private static StubPlan CreateStubPlan(
        string? namespaceName,
        string typeName,
        string? typeParameters,
        StubKind kind)
    {
        return new StubPlan
        {
            Identity = GetStubIdentity(namespaceName, typeName),
            NamespaceName = namespaceName,
            TypeName = typeName,
            TypeParameters = typeParameters,
            Kind = kind,
        };
    }

    private static string? GetContainingNamespace(INamedTypeSymbol type)
    {
        return type.ContainingNamespace is { IsGlobalNamespace: false }
            ? type.ContainingNamespace.ToDisplayString()
            : null;
    }

    private static string GetRequiredFullName(Type type)
    {
        return type.FullName
            ?? throw new InvalidOperationException("Resolved external type must have a full name.");
    }

    private static string GetAssemblyName(Type type)
    {
        return type.Assembly.GetName().Name
            ?? throw new InvalidOperationException("Resolved external generic type must have an assembly name.");
    }

    private static INamedTypeSymbol? FindAccessibleNonGenericSibling(INamedTypeSymbol type)
    {
        if (type.ContainingType is not null)
        {
            return null;
        }

        var sibling = type.ContainingNamespace
            .GetTypeMembers(type.Name)
            .SingleOrDefault(static candidate => candidate.Arity == 0);

        return sibling is not null && IsSiblingUsableWithoutStub(sibling)
            ? sibling
            : null;
    }

    private static INamedTypeSymbol? FindAccessibleReferencedNonGenericSibling(
        Compilation compilation,
        string assemblyName,
        string fullTypeName)
    {
        var sibling = ReferencedTypeSymbolLocator.FindReferencedTypeSymbol(compilation, assemblyName, fullTypeName);
        return sibling is not null && IsSiblingUsableWithoutStub(sibling)
            ? sibling
            : null;
    }

    private static bool IsSiblingUsableWithoutStub(INamedTypeSymbol sibling)
    {
        return sibling.DeclaredAccessibility == Accessibility.Public ||
               sibling.Locations.Any(static location => location.IsInSource);
    }

    private static string GetStubIdentity(string? namespaceName, string typeName)
    {
        return string.IsNullOrEmpty(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
    }
}
