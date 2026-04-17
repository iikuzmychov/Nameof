using System;
using Microsoft.CodeAnalysis;
using Nameof.Internal.Support;

namespace Nameof.Internal.Model;

internal sealed record RequestTarget
{
    public INamedTypeSymbol? Symbol { get; init; }
    public string? FullTypeName { get; init; }
    public INamedTypeSymbol? AssemblyOfType { get; init; }
    public string? AssemblyName { get; init; }
    public bool IsSymbolTarget => Symbol is not null;
    public bool IsFullNameTarget => FullTypeName is not null;
    public bool HasAssemblyName => AssemblyName is not null;
    public string RequestedAssemblyName =>
        Symbol?.ContainingAssembly.Identity.Name ??
        AssemblyOfType?.ContainingAssembly.Identity.Name ??
        AssemblyName ??
        string.Empty;
    public string DiagnosticDisplayName =>
        Symbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ??
        FullTypeName ??
        string.Empty;
    public string DuplicateAssemblyTargetDisplayName =>
        Symbol?.ContainingAssembly.Identity.Name ??
        AssemblyOfType?.ContainingAssembly.Identity.Name ??
        AssemblyName ??
        string.Empty;
    public string DeduplicationKey =>
        Symbol is not null
            ? $"{Symbol.ContainingAssembly.Identity.Name}|{TypeNameUtilities.GetMetadataFullName(Symbol)}"
            : $"{DuplicateAssemblyTargetDisplayName}|{FullTypeName}";

    public static RequestTarget ForSymbol(INamedTypeSymbol symbol)
        => new()
        {
            Symbol = symbol,
        };

    public static RequestTarget ForFullNameWithAssemblyOfType(string fullTypeName, INamedTypeSymbol assemblyOfType)
        => new()
        {
            FullTypeName = fullTypeName,
            AssemblyOfType = assemblyOfType,
        };

    public static RequestTarget ForFullNameWithAssemblyName(string fullTypeName, string assemblyName)
        => new()
        {
            FullTypeName = fullTypeName,
            AssemblyName = assemblyName,
        };
}
