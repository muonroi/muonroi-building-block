using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Muonroi.RuleEngine.SourceGenerators.Diagnostics;

internal static class RuleAuthoringAnalyzer
{
    private sealed class RuleMethodInfo
    {
        public string Code { get; }
        public int Order { get; }
        public IReadOnlyList<string> DependsOn { get; }
        public MethodDeclarationSyntax MethodSyntax { get; }
        public Location Location { get; }
        public HashSet<string> ConsumedFactKeys { get; }
        public HashSet<string> ProducedFactKeys { get; }

        public RuleMethodInfo(
            string code,
            int order,
            IReadOnlyList<string> dependsOn,
            MethodDeclarationSyntax methodSyntax,
            Location location,
            HashSet<string> consumedFactKeys,
            HashSet<string> producedFactKeys)
        {
            Code = code;
            Order = order;
            DependsOn = dependsOn;
            MethodSyntax = methodSyntax;
            Location = location;
            ConsumedFactKeys = consumedFactKeys;
            ProducedFactKeys = producedFactKeys;
        }
    }

    public static void Analyze(Compilation compilation, IEnumerable<MethodDeclarationSyntax> methods, SourceProductionContext context)
    {
        Analyze(compilation, methods, context.ReportDiagnostic);
    }

    public static void Analyze(Compilation compilation, IEnumerable<MethodDeclarationSyntax> methods, Action<Diagnostic> reportDiagnostic)
    {
        List<RuleMethodInfo> infos = [];

        foreach (MethodDeclarationSyntax method in methods)
        {
            SemanticModel model = compilation.GetSemanticModel(method.SyntaxTree);
            IMethodSymbol? methodSymbol = model.GetDeclaredSymbol(method);
            if (methodSymbol is null)
            {
                continue;
            }

            AttributeData? extractAttribute = methodSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name.IndexOf("ExtractAsRule", StringComparison.Ordinal) >= 0);
            if (extractAttribute is null)
            {
                continue;
            }

            (string code, int order, IReadOnlyList<string> dependsOn) = ParseRuleAttribute(extractAttribute, methodSymbol.Name);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            HashSet<string> consumed = ExtractConsumedFactKeys(method, model);
            HashSet<string> produced = ExtractProducedFactKeys(method, model);

            infos.Add(new RuleMethodInfo(
                code,
                order,
                dependsOn,
                method,
                method.GetLocation(),
                consumed,
                produced));

            if (order > 1 && dependsOn.Count == 0)
            {
                reportDiagnostic(Diagnostic.Create(
                    RuleGenDiagnostics.OrderWithoutDependsOn,
                    method.Identifier.GetLocation(),
                    code,
                    order));
            }

            AnalyzeNullableStringAssignments(method, model, reportDiagnostic);
            AnalyzeFactGuardThrowPattern(method, model, code, reportDiagnostic);
        }

        if (infos.Count == 0)
        {
            return;
        }

        Dictionary<string, RuleMethodInfo> ruleByCode = infos
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (RuleMethodInfo info in infos)
        {
            foreach (string dep in info.DependsOn)
            {
                if (!ruleByCode.ContainsKey(dep))
                {
                    reportDiagnostic(Diagnostic.Create(
                        RuleGenDiagnostics.MissingDependencyReference,
                        info.MethodSyntax.Identifier.GetLocation(),
                        info.Code,
                        dep));
                }
            }
        }

        Dictionary<string, List<RuleMethodInfo>> producerByKey = infos
            .SelectMany(rule => rule.ProducedFactKeys.Select(key => new { key, rule }))
            .GroupBy(x => x.key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.rule).ToList(),
                StringComparer.Ordinal);

        foreach (RuleMethodInfo info in infos)
        {
            HashSet<string> reachableDeps = GetTransitiveDependencies(info.Code, ruleByCode);
            foreach (string key in info.ConsumedFactKeys)
            {
                if (!producerByKey.TryGetValue(key, out List<RuleMethodInfo>? producers) || producers.Count == 0)
                {
                    continue;
                }

                bool hasDependencyPath = producers.Any(p =>
                    !string.Equals(p.Code, info.Code, StringComparison.OrdinalIgnoreCase) &&
                    reachableDeps.Contains(p.Code));

                if (hasDependencyPath)
                {
                    continue;
                }

                string producerCodes = string.Join(", ", producers
                    .Where(p => !string.Equals(p.Code, info.Code, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(producerCodes))
                {
                    continue;
                }

                reportDiagnostic(Diagnostic.Create(
                    RuleGenDiagnostics.FactConsumptionWithoutDependency,
                    info.MethodSyntax.Identifier.GetLocation(),
                    info.Code,
                    key,
                    producerCodes));
            }
        }
    }

    private static (string Code, int Order, IReadOnlyList<string> DependsOn) ParseRuleAttribute(AttributeData attribute, string fallbackName)
    {
        string code = fallbackName;
        int order = 0;
        List<string> dependsOn = [];

        if (attribute.ConstructorArguments.Length > 0)
        {
            code = attribute.ConstructorArguments[0].Value?.ToString() ?? fallbackName;
        }

        foreach (KeyValuePair<string, TypedConstant> arg in attribute.NamedArguments)
        {
            if (string.Equals(arg.Key, "Order", StringComparison.OrdinalIgnoreCase))
            {
                order = (int)(arg.Value.Value ?? 0);
            }
            else if (string.Equals(arg.Key, "DependsOn", StringComparison.OrdinalIgnoreCase) && arg.Value.Kind == TypedConstantKind.Array)
            {
                dependsOn.AddRange(arg.Value.Values
                    .Select(v => v.Value?.ToString())
                    .Where(v => !string.IsNullOrWhiteSpace(v))!
                    .Select(v => v!));
            }
        }

        return (code, order, dependsOn);
    }

    private static HashSet<string> ExtractConsumedFactKeys(MethodDeclarationSyntax method, SemanticModel model)
    {
        HashSet<string> result = new(StringComparer.Ordinal);

        foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Get", StringComparison.Ordinal) ||
                memberAccess.Expression is not IdentifierNameSyntax objectName ||
                !string.Equals(objectName.Identifier.ValueText, "facts", StringComparison.Ordinal))
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            ExpressionSyntax arg = invocation.ArgumentList.Arguments[0].Expression;
            if (TryResolveStringValue(arg, model, out string key))
            {
                result.Add(key);
            }
        }

        return result;
    }

    private static HashSet<string> ExtractProducedFactKeys(MethodDeclarationSyntax method, SemanticModel model)
    {
        HashSet<string> result = new(StringComparer.Ordinal);

        foreach (AssignmentExpressionSyntax assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not ElementAccessExpressionSyntax elementAccess)
            {
                continue;
            }

            if (elementAccess.Expression is not IdentifierNameSyntax objectName ||
                !string.Equals(objectName.Identifier.ValueText, "facts", StringComparison.Ordinal) ||
                elementAccess.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            ExpressionSyntax arg = elementAccess.ArgumentList.Arguments[0].Expression;
            if (TryResolveStringValue(arg, model, out string key))
            {
                result.Add(key);
            }
        }

        return result;
    }

    private static void AnalyzeNullableStringAssignments(
        MethodDeclarationSyntax method,
        SemanticModel model,
        Action<Diagnostic> reportDiagnostic)
    {
        foreach (AssignmentExpressionSyntax assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            ISymbol? symbol = model.GetSymbolInfo(assignment.Left).Symbol;
            if (symbol is not IPropertySymbol property)
            {
                continue;
            }

            if (property.Type.SpecialType != SpecialType.System_String ||
                property.NullableAnnotation != NullableAnnotation.NotAnnotated)
            {
                continue;
            }

            if (IsSafeStringAssignment(assignment.Right, model))
            {
                continue;
            }

            TypeInfo rightTypeInfo = model.GetTypeInfo(assignment.Right);
            if (rightTypeInfo.Nullability.FlowState != NullableFlowState.MaybeNull &&
                rightTypeInfo.Nullability.Annotation != NullableAnnotation.Annotated)
            {
                continue;
            }

            reportDiagnostic(Diagnostic.Create(
                RuleGenDiagnostics.NullableAssignedToNonNullableString,
                assignment.GetLocation(),
                property.Name,
                assignment.Right.ToString()));
        }
    }

    private static void AnalyzeFactGuardThrowPattern(
        MethodDeclarationSyntax method,
        SemanticModel model,
        string ruleCode,
        Action<Diagnostic> reportDiagnostic)
    {
        foreach (BinaryExpressionSyntax coalesce in method.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (!coalesce.IsKind(SyntaxKind.CoalesceExpression))
            {
                continue;
            }

            if (coalesce.Right is not ThrowExpressionSyntax throwExpression ||
                throwExpression.Expression is not ObjectCreationExpressionSyntax objectCreation ||
                objectCreation.Type is not IdentifierNameSyntax typeName ||
                !string.Equals(typeName.Identifier.ValueText, nameof(InvalidOperationException), StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryGetFactKeyFromGetInvocation(coalesce.Left, model, out string factKey))
            {
                continue;
            }

            reportDiagnostic(Diagnostic.Create(
                RuleGenDiagnostics.FactGuardThrowsInvalidOperation,
                throwExpression.GetLocation(),
                ruleCode,
                factKey));
        }
    }

    private static bool TryGetFactKeyFromGetInvocation(ExpressionSyntax expression, SemanticModel model, out string factKey)
    {
        factKey = string.Empty;
        if (expression is not InvocationExpressionSyntax invocation ||
            invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !string.Equals(memberAccess.Name.Identifier.ValueText, "Get", StringComparison.Ordinal) ||
            memberAccess.Expression is not IdentifierNameSyntax objectName ||
            !string.Equals(objectName.Identifier.ValueText, "facts", StringComparison.Ordinal) ||
            invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        return TryResolveStringValue(invocation.ArgumentList.Arguments[0].Expression, model, out factKey);
    }

    private static bool IsSafeStringAssignment(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return true;
        }

        if (expression is InterpolatedStringExpressionSyntax)
        {
            return true;
        }

        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.CoalesceExpression))
        {
            return true;
        }

        Optional<object?> constant = model.GetConstantValue(expression);
        return constant.HasValue && constant.Value is string;
    }

    private static bool TryResolveStringValue(ExpressionSyntax expression, SemanticModel model, out string value)
    {
        Optional<object?> constantValue = model.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is string stringValue)
        {
            value = stringValue;
            return true;
        }

        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        ISymbol? symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol is IFieldSymbol fieldSymbol && fieldSymbol.IsConst && fieldSymbol.ConstantValue is string constString)
        {
            value = constString;
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax identifier &&
            string.Equals(identifier.Identifier.ValueText, "nameof", StringComparison.Ordinal) &&
            invocation.ArgumentList.Arguments.Count > 0)
        {
            value = invocation.ArgumentList.Arguments[0].Expression.ToString().Split('.').Last();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static HashSet<string> GetTransitiveDependencies(
        string ruleCode,
        IReadOnlyDictionary<string, RuleMethodInfo> ruleByCode)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Stack<string> stack = new();
        stack.Push(ruleCode);

        while (stack.Count > 0)
        {
            string current = stack.Pop();
            if (!ruleByCode.TryGetValue(current, out RuleMethodInfo? info))
            {
                continue;
            }

            foreach (string dep in info.DependsOn)
            {
                if (visited.Add(dep))
                {
                    stack.Push(dep);
                }
            }
        }

        return visited;
    }
}

