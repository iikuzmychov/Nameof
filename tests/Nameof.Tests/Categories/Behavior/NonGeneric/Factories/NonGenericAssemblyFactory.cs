using Microsoft.CodeAnalysis;
using Nameof.Tests.Categories.Behavior.NonGeneric.Model;
using Nameof.Tests.Infrastructure;
using Nameof.Tests.Infrastructure.Model;

namespace Nameof.Tests.Categories.Behavior.NonGeneric.Factories;

internal static class NonGenericAssemblyFactory
{
    internal static ExternalFixture CreateExternalFixture(
        string assemblyName,
        DeclarationType declarationType,
        AccessType accessType,
        string typeName,
        bool includeAnchor = false,
        string? anchorTypeName = null)
    {
        var source = NonGenericSourceFactory.CreateExternalDeclarationAssemblySource(
            declarationType,
            accessType,
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
        DeclarationType declarationType,
        string typeName)
    {
        var declaration = NonGenericSourceFactory.CreateDecoyDeclaration(declarationType, typeName);
        var source =
            $$"""
            namespace ExternalFixtures;

            {{declaration}}
            """;

        return InMemoryAssemblyReferenceFactory.Create(assemblyName, source);
    }
}
