using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nameof;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NameofStubUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor StubUsageRule = new(
        id: "NAMEOF006",
        title: "Invalid Nameof stub usage",
        messageFormat: "Generated Nameof stub type '{0}' may only be used in nameof<T> or nameof(...) expressions",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [StubUsageRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeTypeName,
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.IdentifierName,
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.GenericName,
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.QualifiedName,
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.AliasQualifiedName);
    }

    private static void AnalyzeTypeName(SyntaxNodeAnalysisContext context)
    {
        if (IsGeneratedSyntaxTree(context.Node.SyntaxTree))
        {
            return;
        }

        if (!IsOutermostTypeName(context.Node))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not INamedTypeSymbol typeSymbol)
        {
            return;
        }

        if (!HasNameofStubAttribute(typeSymbol))
        {
            return;
        }

        if (IsAllowedGenericNameofTypeArgument(context.Node, context.SemanticModel, context.CancellationToken) ||
            IsInsideNameofExpression(context.Node))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            StubUsageRule,
            context.Node.GetLocation(),
            typeSymbol.Name));
    }

    private static bool IsGeneratedSyntaxTree(SyntaxTree syntaxTree)
    {
        return syntaxTree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOutermostTypeName(SyntaxNode node)
    {
        return node.Parent is not NameSyntax;
    }

    private static bool HasNameofStubAttribute(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass is { Name: "NameofStubAttribute" } attributeClass &&
            attributeClass.ContainingNamespace.ToDisplayString() == "Nameof");
    }

    private static bool IsAllowedGenericNameofTypeArgument(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (node.Parent is not TypeArgumentListSyntax typeArgumentList ||
            typeArgumentList.Parent is not GenericNameSyntax genericName ||
            !string.Equals(genericName.Identifier.ValueText, "nameof", StringComparison.Ordinal))
        {
            return false;
        }

        return semanticModel.GetSymbolInfo(genericName, cancellationToken).Symbol is INamedTypeSymbol genericType &&
               genericType.Name == "nameof" &&
               genericType.ContainingNamespace.ToDisplayString() == "Nameof";
    }

    private static bool IsInsideNameofExpression(SyntaxNode node)
    {
        return node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().Any(static invocation =>
            invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" });
    }
}
