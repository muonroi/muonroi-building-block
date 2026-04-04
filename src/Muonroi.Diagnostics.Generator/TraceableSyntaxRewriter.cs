using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Muonroi.Diagnostics.Generator;

internal sealed class TraceableSyntaxRewriter(SemanticModel semanticModel) : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel = semanticModel;
    private int _captureCount = 0;
    private const int MaxCaptures = 50;

    public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        var result = base.VisitAssignmentExpression(node);
        if (result is not AssignmentExpressionSyntax assignment) return result;
        if (_captureCount >= MaxCaptures) return result;

        // Only capture simple local variables or properties
        if (assignment.Left is IdentifierNameSyntax or MemberAccessExpressionSyntax)
        {
            _captureCount++;
            return CreateCaptureExpression(assignment);
        }

        return result;
    }

    public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        var result = base.VisitLocalDeclarationStatement(node);
        if (result is not LocalDeclarationStatementSyntax declaration) return result;
        if (_captureCount >= MaxCaptures) return result;

        var variables = declaration.Declaration.Variables;
        if (variables.Count == 1 && variables[0].Initializer != null)
        {
            _captureCount++;
            return SyntaxFactory.Block(
                declaration,
                CreateCaptureStatement(variables[0].Identifier.Text)
            );
        }

        return result;
    }

    private ExpressionSyntax CreateCaptureExpression(AssignmentExpressionSyntax assignment)
    {
        var varName = assignment.Left.ToString();
        return SyntaxFactory.ParseExpression(
            $"((Func<object?, object?>)((v) => {{ Muonroi.Core.Abstractions.Context.MTraceContextHolder.Current.Value?.RecordLineTrace({GetLine(assignment)}, \"{varName}\", v); return v; }}))({assignment.ToString()})");
    }

    private StatementSyntax CreateCaptureStatement(string varName)
    {
        return SyntaxFactory.ParseStatement(
            $"Muonroi.Core.Abstractions.Context.MTraceContextHolder.Current.Value?.RecordLineTrace(0, \"{varName}\", {varName});");
    }

    private int GetLine(SyntaxNode node) => node.GetLocation().GetMappedLineSpan().StartLinePosition.Line + 1;
}
