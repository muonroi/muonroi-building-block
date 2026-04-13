using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// MBB008: Detects cross-capability type references inside AddM* extension methods
/// that are not protected by an IMEcosystemRegistry.Has(MCapability.X) guard.
///
/// Purpose: Enforce the "better together" pattern at compile-time. Each capability
/// (Logging, RuleEngine, MultiTenant, Auth) must check registry.Has() before accessing
/// types from a different capability anchor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb008_CrossCapabilityAnalyzer : DiagnosticAnalyzer
{
    // Maps capability name → namespace prefixes that belong to that capability
    private static readonly Dictionary<string, string[]> CapabilityNamespaces = new Dictionary<string, string[]>
    {
        ["Logging"]     = new[] { "Muonroi.Logging", "Muonroi.Observability" },
        ["RuleEngine"]  = new[] { "Muonroi.RuleEngine", "Muonroi.Rules" },
        ["MultiTenant"] = new[] { "Muonroi.Tenancy", "Muonroi.MultiTenant" },
        ["Auth"]        = new[] { "Muonroi.Auth", "Muonroi.Governance" },
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB008CrossCapabilityDirectReference);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // Analyze identifier name references (type references)
        context.RegisterSyntaxNodeAction(AnalyzeIdentifierName, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeIdentifierName(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not IdentifierNameSyntax identifier)
        {
            return;
        }

        // Must be inside an AddM* method
        MethodDeclarationSyntax? containingMethod = identifier.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (containingMethod is null)
        {
            return;
        }

        string methodName = containingMethod.Identifier.ValueText;
        if (!methodName.StartsWith("AddM", System.StringComparison.Ordinal))
        {
            return;
        }

        // Get semantic info for the identifier to find the referenced type's namespace
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(identifier);
        ISymbol? symbol = symbolInfo.Symbol;
        if (symbol is null)
        {
            return;
        }

        // Only care about type symbols (interfaces, classes, structs, enums)
        INamedTypeSymbol? typeSymbol = symbol as INamedTypeSymbol
            ?? (symbol as IMethodSymbol)?.ContainingType
            ?? (symbol as IPropertySymbol)?.ContainingType
            ?? (symbol as IFieldSymbol)?.ContainingType;

        if (typeSymbol is null)
        {
            return;
        }

        string referencedNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (string.IsNullOrEmpty(referencedNamespace))
        {
            return;
        }

        string? referencedCapability = GetCapabilityForNamespace(referencedNamespace);
        if (referencedCapability is null)
        {
            return;
        }

        // Determine the method's own capability from its containing namespace
        string methodNamespace = GetContainingNamespace(containingMethod);
        string? methodCapability = GetCapabilityForNamespace(methodNamespace);

        // Same capability → no warning
        if (referencedCapability == methodCapability)
        {
            return;
        }

        // Check whether this reference is inside a registry.Has(MCapability.{referencedCapability}) guard
        if (IsInsideHasGuard(identifier, referencedCapability))
        {
            return;
        }

        // Report MBB008
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add("Capability", referencedCapability);

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB008CrossCapabilityDirectReference,
            identifier.GetLocation(),
            properties,
            typeSymbol.Name,
            referencedCapability,
            methodName));
    }

    /// <summary>
    /// Determines which capability anchor owns the given namespace.
    /// Returns null if the namespace doesn't belong to any capability.
    /// </summary>
    private static string? GetCapabilityForNamespace(string namespaceName)
    {
        foreach (KeyValuePair<string, string[]> entry in CapabilityNamespaces)
        {
            string[] prefixes = entry.Value;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (namespaceName.Equals(prefix, System.StringComparison.Ordinal) ||
                    namespaceName.StartsWith(prefix + ".", System.StringComparison.Ordinal))
                {
                    return entry.Key;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the namespace string of the containing method's declaration.
    /// </summary>
    private static string GetContainingNamespace(MethodDeclarationSyntax method)
    {
        BaseNamespaceDeclarationSyntax? ns = method.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        return ns?.Name.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Checks whether the given node is inside an if-block whose condition contains
    /// a call to Has(MCapability.{requiredCapability}).
    /// </summary>
    private static bool IsInsideHasGuard(SyntaxNode node, string requiredCapability)
    {
        SyntaxNode? current = node.Parent;
        while (current != null)
        {
            if (current is IfStatementSyntax ifStatement)
            {
                string conditionText = ifStatement.Condition.ToString();
                // Accept both "Has(MCapability.X)" and ".Has(MCapability.X)"
                if (conditionText.IndexOf("Has(MCapability." + requiredCapability + ")", System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            // Stop walking up at method boundary
            if (current is MethodDeclarationSyntax)
            {
                break;
            }

            current = current.Parent;
        }
        return false;
    }
}
