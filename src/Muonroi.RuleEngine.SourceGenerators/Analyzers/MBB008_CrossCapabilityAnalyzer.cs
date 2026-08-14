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
    // Maps capability name → namespace prefixes that belong to that capability.
    // Mirrors the MCapability enum and the runtime Register(MCapability.X) calls:
    // Governance is its own anchor (LicenseServiceCollectionExtensions registers
    // MCapability.Governance), so it is NOT folded into Auth.
    private static readonly Dictionary<string, string[]> CapabilityNamespaces = new Dictionary<string, string[]>
    {
        ["Logging"]     = new[] { "Muonroi.Logging", "Muonroi.Observability" },
        ["RuleEngine"]  = new[] { "Muonroi.RuleEngine", "Muonroi.Rules" },
        ["MultiTenant"] = new[] { "Muonroi.Tenancy", "Muonroi.MultiTenant" },
        ["Auth"]        = new[] { "Muonroi.Auth" },
        ["Governance"]  = new[] { "Muonroi.Governance" },
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
    /// Checks whether the given node is dominated by a guard for the required capability.
    /// Unlike a textual match on the condition, this walks the syntax tree and matches a
    /// real <c>Has(MCapability.{requiredCapability})</c> invocation, so it is not fooled by
    /// formatting and does not pass on an unrelated condition that merely contains the text.
    ///
    /// Recognised guard shapes:
    ///   • <c>if (registry.Has(MCapability.X)) { ...use... }</c>
    ///   • <c>registry.Has(MCapability.X) &amp;&amp; ...use...</c> (logical-and short-circuit)
    ///   • <c>registry.Has(MCapability.X) ? ...use... : ...</c> (ternary condition)
    ///   • <c>if (!registry.Has(MCapability.X)) return;/throw;</c> then ...use... (negated early-exit)
    ///
    /// Limitation: cross-method data-flow (a guard delegated to a helper method) is NOT
    /// resolved — that requires full flow analysis and is out of scope for this analyzer.
    /// </summary>
    private static bool IsInsideHasGuard(SyntaxNode node, string requiredCapability)
    {
        SyntaxNode? current = node.Parent;
        while (current != null)
        {
            if (current is IfStatementSyntax ifStatement
                && ConditionGuards(ifStatement.Condition, requiredCapability))
            {
                return true;
            }

            if (current is ConditionalExpressionSyntax ternary
                && ConditionGuards(ternary.Condition, requiredCapability))
            {
                return true;
            }

            if (current is BinaryExpressionSyntax binary
                && binary.IsKind(SyntaxKind.LogicalAndExpression)
                && ConditionGuards(binary, requiredCapability))
            {
                return true;
            }

            // Stop walking up at method boundary
            if (current is MethodDeclarationSyntax)
            {
                break;
            }

            current = current.Parent;
        }

        return HasPrecedingNegatedExitGuard(node, requiredCapability);
    }

    /// <summary>
    /// Returns true when <paramref name="condition"/> contains a real
    /// <c>Has(MCapability.{requiredCapability})</c> invocation anywhere within it.
    /// </summary>
    private static bool ConditionGuards(ExpressionSyntax condition, string requiredCapability)
    {
        foreach (InvocationExpressionSyntax invocation in
                 condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (IsHasCapabilityCall(invocation, requiredCapability))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Matches an invocation of a method named <c>Has</c> whose single argument is
    /// <c>MCapability.{requiredCapability}</c>. Accepts both <c>registry.Has(...)</c> and a
    /// bare <c>Has(...)</c>; the capability member name must match exactly.
    /// </summary>
    private static bool IsHasCapabilityCall(InvocationExpressionSyntax invocation, string requiredCapability)
    {
        string? methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

        if (methodName != "Has" || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        ExpressionSyntax argument = invocation.ArgumentList.Arguments[0].Expression;
        return argument is MemberAccessExpressionSyntax capabilityAccess
            && capabilityAccess.Name.Identifier.ValueText == requiredCapability
            && IsMCapabilityReceiver(capabilityAccess.Expression);
    }

    private static bool IsMCapabilityReceiver(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "MCapability",
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText == "MCapability",
            _ => false
        };
    }

    /// <summary>
    /// Detects the negated early-exit guard idiom — a preceding sibling statement
    /// <c>if (!registry.Has(MCapability.X)) return;/throw;</c> in the same or an enclosing
    /// block, which guarantees the capability is present for code that follows.
    /// </summary>
    private static bool HasPrecedingNegatedExitGuard(SyntaxNode node, string requiredCapability)
    {
        foreach (BlockSyntax block in node.Ancestors().OfType<BlockSyntax>())
        {
            StatementSyntax? owning = null;
            foreach (StatementSyntax statement in block.Statements)
            {
                if (statement.FullSpan.Contains(node.SpanStart))
                {
                    owning = statement;
                    break;
                }
            }

            if (owning is null)
            {
                continue;
            }

            foreach (StatementSyntax statement in block.Statements)
            {
                if (statement == owning)
                {
                    break;
                }

                if (statement is IfStatementSyntax ifStatement
                    && IsNegatedHasGuardWithExit(ifStatement, requiredCapability))
                {
                    return true;
                }
            }

            if (block.Parent is MethodDeclarationSyntax)
            {
                break;
            }
        }

        return false;
    }

    private static bool IsNegatedHasGuardWithExit(IfStatementSyntax ifStatement, string requiredCapability)
    {
        if (ifStatement.Condition is not PrefixUnaryExpressionSyntax negation
            || !negation.IsKind(SyntaxKind.LogicalNotExpression)
            || !ConditionGuards(negation.Operand, requiredCapability))
        {
            return false;
        }

        return StatementExits(ifStatement.Statement);
    }

    private static bool StatementExits(StatementSyntax statement)
    {
        if (statement is ReturnStatementSyntax || statement is ThrowStatementSyntax)
        {
            return true;
        }

        if (statement is BlockSyntax block && block.Statements.Count > 0)
        {
            StatementSyntax last = block.Statements[block.Statements.Count - 1];
            return last is ReturnStatementSyntax || last is ThrowStatementSyntax;
        }

        return false;
    }
}
