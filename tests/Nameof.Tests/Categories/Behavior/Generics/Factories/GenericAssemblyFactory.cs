using Microsoft.CodeAnalysis;
using Nameof.Tests.Categories.Behavior.Generics.Model;
using Nameof.Tests.Infrastructure;
using Nameof.Tests.Infrastructure.Model;

namespace Nameof.Tests.Categories.Behavior.Generics.Factories;

internal static class GenericAssemblyFactory
{
    internal static ExternalFixture CreateExternalFixture(
        string assemblyName,
        GenericDeclarationType declarationType,
        AccessType accessType,
        Arity arity,
        string typeName,
        bool includeAnchor = false,
        string? anchorTypeName = null)
    {
        var source = GenericSourceFactory.CreateExternalDeclarationAssemblySource(
            declarationType,
            accessType,
            typeName,
            arity,
            includeAnchor,
            anchorTypeName);

        return new ExternalFixture
        {
            Reference = InMemoryAssemblyReferenceFactory.Create(assemblyName, source),
            TypeName = typeName,
            AssemblyName = assemblyName,
        };
    }

    internal static ExternalFixture CreateCoexistenceFixture(
        string assemblyName,
        AccessType nonGenericAccessType,
        AccessType genericAccessType,
        string typeName,
        bool includeAnchor = false,
        string? anchorTypeName = null)
    {
        var source = GenericSourceFactory.CreateExternalCoexistenceAssemblySource(
            nonGenericAccessType,
            genericAccessType,
            typeName,
            includeAnchor,
            anchorTypeName);

        return new ExternalFixture
        {
            Reference = InMemoryAssemblyReferenceFactory.Create(assemblyName, source),
            TypeName = typeName,
            AssemblyName = assemblyName,
        };
    }

    internal static MetadataReference CreateDecoyFixture(
        string assemblyName,
        GenericDeclarationType declarationType,
        AccessType accessType,
        Arity arity,
        string typeName)
    {
        var source = GenericSourceFactory.CreateExternalDeclarationAssemblySource(
            declarationType,
            accessType,
            typeName,
            arity,
            includeAnchor: false,
            anchorTypeName: null);

        return InMemoryAssemblyReferenceFactory.Create(assemblyName, source);
    }

    internal static MetadataReference CreateCoexistenceDecoyFixture(
        string assemblyName,
        AccessType nonGenericAccessType,
        AccessType genericAccessType,
        string typeName)
    {
        var source = GenericSourceFactory.CreateExternalCoexistenceDecoyAssemblySource(
            genericAccessType,
            typeName);

        return InMemoryAssemblyReferenceFactory.Create(assemblyName, source);
    }
}
