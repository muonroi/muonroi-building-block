using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace Muonroi.RuleGen.Services;

internal static class CSharpRulePatternService
{
    public static (string ConditionFeel, string ActionFeel, bool IsCustom) ExtractConditionAndAction(string? methodBody)
    {
        if (string.IsNullOrWhiteSpace(methodBody))
        {
            return ("true", string.Empty, true);
        }

        string wrapped = $"class __Dummy {{ void __Rule() {methodBody} }}";
        CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(wrapped).GetCompilationUnitRoot();
        MethodDeclarationSyntax? method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method?.Body is null)
        {
            return ("true", string.Empty, true);
        }

        IfStatementSyntax? ifStmt = method.Body.Statements.OfType<IfStatementSyntax>().FirstOrDefault();
        if (ifStmt is null)
        {
            string actionOnly = ExtractFactAssignments(method.Body);
            return ("true", actionOnly, true);
        }

        string ifCondition = ifStmt.Condition.ToString();
        bool hasFailureInIf = ifStmt.Statement.ToString().Contains("RuleResult.Failure", StringComparison.Ordinal);
        string condition = hasFailureInIf
            ? $"!({ifCondition})"
            : ifCondition;

        string feelCondition = FeelCSharpTranslator.CSharpToFeel(condition);
        string action = ExtractFactAssignments(method.Body);
        bool isCustom = method.Body.DescendantNodes().OfType<ForEachStatementSyntax>().Any() ||
                       method.Body.DescendantNodes().OfType<ForStatementSyntax>().Any() ||
                       method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                           .Any(i => Regex.IsMatch(i.Expression.ToString(), @"^(_|this\.)", RegexOptions.CultureInvariant));

        return (feelCondition, action, isCustom);
    }

    private static string ExtractFactAssignments(BlockSyntax body)
    {
        List<string> assignments = [];
        foreach (AssignmentExpressionSyntax assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not ElementAccessExpressionSyntax elementAccess)
            {
                continue;
            }

            if (!string.Equals(elementAccess.Expression.ToString(), "facts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? index = elementAccess.ArgumentList.Arguments.FirstOrDefault()?.ToString();
            if (string.IsNullOrWhiteSpace(index))
            {
                continue;
            }

            string key = index.Trim().Trim('"', '\'');
            string valueFeel = FeelCSharpTranslator.CSharpToFeel(assignment.Right.ToString());
            assignments.Add($"facts['{key}'] = {valueFeel}");
        }

        return string.Join("; ", assignments);
    }
}
