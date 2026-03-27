using System;
using System.Collections.Generic;

namespace Muonroi.RuleEngine.SourceGenerators;

internal static class FeelExpressionSyntaxValidator
{
    public static bool TryValidate(string expression, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Expression cannot be empty.";
            return false;
        }

        int roundDepth = 0;
        int squareDepth = 0;
        bool expectOperand = true;
        TokenKind previous = TokenKind.None;
        foreach (Token token in Tokenize(expression))
        {
            switch (token.Kind)
            {
                case TokenKind.LeftParen:
                    roundDepth++;
                    break;
                case TokenKind.RightParen:
                    roundDepth--;
                    if (roundDepth < 0)
                    {
                        error = "Unexpected closing parenthesis.";
                        return false;
                    }
                    break;
                case TokenKind.LeftBracket:
                    squareDepth++;
                    break;
                case TokenKind.RightBracket:
                    squareDepth--;
                    if (squareDepth < 0)
                    {
                        error = "Unexpected closing bracket.";
                        return false;
                    }
                    break;
            }

            if (token.Kind == TokenKind.Operator)
            {
                if (expectOperand && !string.Equals(token.Text, "not", StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Unexpected operator '{token.Text}'.";
                    return false;
                }

                expectOperand = true;
            }
            else if (token.Kind == TokenKind.Comma || token.Kind == TokenKind.Range)
            {
                if (expectOperand)
                {
                    error = $"Unexpected token '{token.Text}'.";
                    return false;
                }

                expectOperand = true;
            }
            else if (token.Kind != TokenKind.RightParen && token.Kind != TokenKind.RightBracket)
            {
                expectOperand = false;
            }

            previous = token.Kind;
        }

        if (roundDepth != 0 || squareDepth != 0)
        {
            error = "Unbalanced parentheses or brackets.";
            return false;
        }

        if (expectOperand && previous != TokenKind.None)
        {
            error = "Expression ends with an incomplete operator.";
            return false;
        }

        return true;
    }

    private static IEnumerable<Token> Tokenize(string expression)
    {
        int index = 0;
        while (index < expression.Length)
        {
            char current = expression[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current is '\'' or '"')
            {
                char quote = current;
                int start = ++index;
                while (index < expression.Length && expression[index] != quote)
                {
                    index++;
                }

                if (index >= expression.Length)
                {
                    yield return new Token(TokenKind.Invalid, "unterminated-string");
                    yield break;
                }

                index++;
                yield return new Token(TokenKind.Value, expression.Substring(start, index - start - 1));
                continue;
            }

            if (char.IsLetterOrDigit(current) || current == '_')
            {
                int start = index;
                index++;
                while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] is '_' or '.'))
                {
                    index++;
                }

                string text = expression.Substring(start, index - start);
                yield return new Token(IsOperator(text) ? TokenKind.Operator : TokenKind.Value, text);
                continue;
            }

            if (current == '.' && index + 1 < expression.Length && expression[index + 1] == '.')
            {
                index += 2;
                yield return new Token(TokenKind.Range, "..");
                continue;
            }

            if ((current == '!' || current == '>' || current == '<') && index + 1 < expression.Length && expression[index + 1] == '=')
            {
                string op = expression.Substring(index, 2);
                index += 2;
                yield return new Token(TokenKind.Operator, op);
                continue;
            }

            switch (current)
            {
                case '(':
                    index++;
                    yield return new Token(TokenKind.LeftParen, "(");
                    break;
                case ')':
                    index++;
                    yield return new Token(TokenKind.RightParen, ")");
                    break;
                case '[':
                    index++;
                    yield return new Token(TokenKind.LeftBracket, "[");
                    break;
                case ']':
                    index++;
                    yield return new Token(TokenKind.RightBracket, "]");
                    break;
                case ',':
                    index++;
                    yield return new Token(TokenKind.Comma, ",");
                    break;
                case '=':
                case '>':
                case '<':
                    index++;
                    yield return new Token(TokenKind.Operator, current.ToString());
                    break;
                default:
                    index++;
                    yield return new Token(TokenKind.Invalid, current.ToString());
                    break;
            }
        }
    }

    private static bool IsOperator(string token)
    {
        return string.Equals(token, "and", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "or", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "not", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "in", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    private enum TokenKind
    {
        None,
        Value,
        Operator,
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Comma,
        Range,
        Invalid
    }
}
