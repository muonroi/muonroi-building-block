using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Muonroi.RuleEngine.Runtime.Compilation.Feel;

/// <summary>
/// Converts parsed FEEL syntax nodes into expression trees backed by runtime helpers.
/// </summary>
internal static class ExpressionTreeVisitor
{
    private static readonly MethodInfo ResolveValueMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(ResolveValue), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ToBooleanMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(ToBoolean), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo CompareMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(Compare), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo InValuesMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(InValues), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo InRangeMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(InRange), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ContainsMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(Contains), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Expression VisitAsBoolean(FeelSyntaxNode node, ParameterExpression varsParameter)
    {
        return node switch
        {
            FeelUnaryNode unary when unary.Operator == FeelUnaryOperator.Not => Expression.Not(VisitAsBoolean(unary.Operand, varsParameter)),
            FeelBinaryNode binary when binary.Operator == FeelBinaryOperator.And => Expression.AndAlso(VisitAsBoolean(binary.Left, varsParameter), VisitAsBoolean(binary.Right, varsParameter)),
            FeelBinaryNode binary when binary.Operator == FeelBinaryOperator.Or => Expression.OrElse(VisitAsBoolean(binary.Left, varsParameter), VisitAsBoolean(binary.Right, varsParameter)),
            FeelBinaryNode binary when binary.Operator is FeelBinaryOperator.Equal
                or FeelBinaryOperator.NotEqual
                or FeelBinaryOperator.GreaterThan
                or FeelBinaryOperator.GreaterThanOrEqual
                or FeelBinaryOperator.LessThan
                or FeelBinaryOperator.LessThanOrEqual => BuildComparison(binary, varsParameter),
            FeelInValuesNode valuesNode => Expression.Call(InValuesMethod, VisitAsObject(valuesNode.Left, varsParameter), BuildValuesArray(valuesNode.Values, varsParameter)),
            FeelRangeNode rangeNode => Expression.Call(InRangeMethod, VisitAsObject(rangeNode.Left, varsParameter), VisitAsObject(rangeNode.Start, varsParameter), VisitAsObject(rangeNode.End, varsParameter)),
            _ => Expression.Call(ToBooleanMethod, VisitAsObject(node, varsParameter))
        };
    }

    private static Expression VisitAsObject(FeelSyntaxNode node, ParameterExpression varsParameter)
    {
        return node switch
        {
            FeelBooleanLiteralNode booleanNode => Expression.Constant(booleanNode.Value, typeof(object)),
            FeelNumberLiteralNode numberNode => Expression.Constant(numberNode.Value, typeof(object)),
            FeelStringLiteralNode stringNode => Expression.Constant(stringNode.Value, typeof(object)),
            FeelNullLiteralNode => Expression.Constant(null, typeof(object)),
            FeelVariableNode variableNode => Expression.Call(ResolveValueMethod, varsParameter, Expression.Constant(variableNode.Path)),
            FeelFunctionCallNode functionNode when string.Equals(functionNode.Name, "contains", StringComparison.OrdinalIgnoreCase) && functionNode.Arguments.Count == 2
                => Expression.Convert(Expression.Call(ContainsMethod, VisitAsObject(functionNode.Arguments[0], varsParameter), VisitAsObject(functionNode.Arguments[1], varsParameter)), typeof(object)),
            FeelUnaryNode unaryNode when unaryNode.Operator == FeelUnaryOperator.Not => Expression.Convert(VisitAsBoolean(unaryNode, varsParameter), typeof(object)),
            FeelBinaryNode binaryNode when binaryNode.Operator is FeelBinaryOperator.And or FeelBinaryOperator.Or
                => Expression.Convert(VisitAsBoolean(binaryNode, varsParameter), typeof(object)),
            FeelBinaryNode binaryNode when binaryNode.Operator is FeelBinaryOperator.Equal
                or FeelBinaryOperator.NotEqual
                or FeelBinaryOperator.GreaterThan
                or FeelBinaryOperator.GreaterThanOrEqual
                or FeelBinaryOperator.LessThan
                or FeelBinaryOperator.LessThanOrEqual => Expression.Convert(BuildComparison(binaryNode, varsParameter), typeof(object)),
            FeelInValuesNode inNode => Expression.Convert(VisitAsBoolean(inNode, varsParameter), typeof(object)),
            FeelRangeNode rangeNode => Expression.Convert(VisitAsBoolean(rangeNode, varsParameter), typeof(object)),
            _ => throw new NotSupportedException($"Unsupported FEEL syntax node '{node.GetType().Name}'.")
        };
    }

    private static Expression BuildComparison(FeelBinaryNode node, ParameterExpression varsParameter)
    {
        Expression comparison = Expression.Call(CompareMethod, VisitAsObject(node.Left, varsParameter), VisitAsObject(node.Right, varsParameter));
        return node.Operator switch
        {
            FeelBinaryOperator.Equal => Expression.Equal(comparison, Expression.Constant(0)),
            FeelBinaryOperator.NotEqual => Expression.NotEqual(comparison, Expression.Constant(0)),
            FeelBinaryOperator.GreaterThan => Expression.GreaterThan(comparison, Expression.Constant(0)),
            FeelBinaryOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(comparison, Expression.Constant(0)),
            FeelBinaryOperator.LessThan => Expression.LessThan(comparison, Expression.Constant(0)),
            FeelBinaryOperator.LessThanOrEqual => Expression.LessThanOrEqual(comparison, Expression.Constant(0)),
            _ => throw new NotSupportedException($"Unsupported comparison operator '{node.Operator}'.")
        };
    }

    private static NewArrayExpression BuildValuesArray(IReadOnlyList<FeelSyntaxNode> values, ParameterExpression varsParameter)
    {
        return Expression.NewArrayInit(typeof(object), values.Select(value => Expression.Convert(VisitAsObject(value, varsParameter), typeof(object))));
    }

    private static object? ResolveValue(IDictionary<string, object> vars, string path)
    {
        if (vars.TryGetValue(path, out object? directValue))
        {
            return directValue;
        }

        object? current = vars;
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is IDictionary<string, object> currentDictionary)
            {
                if (!currentDictionary.TryGetValue(segment, out current))
                {
                    KeyValuePair<string, object> matched = currentDictionary.FirstOrDefault(pair => string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase));
                    current = string.IsNullOrWhiteSpace(matched.Key) ? null : matched.Value;
                }
            }
            else if (current is not null)
            {
                PropertyInfo? property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                current = property?.GetValue(current);
            }

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static bool ToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool boolean => boolean,
            string text when bool.TryParse(text, out bool parsed) => parsed,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) => Math.Abs(number) > double.Epsilon,
            IConvertible convertible => TryConvertToDouble(convertible, out double converted) && Math.Abs(converted) > double.Epsilon,
            _ => true
        };
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is DateTime leftDateTime && right is DateTime rightDateTime)
        {
            return leftDateTime.CompareTo(rightDateTime);
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool.CompareTo(rightBool);
        }

        if (TryConvertToDouble(left, out double leftNumber) && TryConvertToDouble(right, out double rightNumber))
        {
            double delta = leftNumber - rightNumber;
            if (Math.Abs(delta) <= double.Epsilon)
            {
                return 0;
            }

            return delta < 0 ? -1 : 1;
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool InValues(object? value, object?[] values)
    {
        return values.Any(item => Compare(value, item) == 0);
    }

    private static bool InRange(object? value, object? start, object? end)
    {
        return Compare(value, start) >= 0 && Compare(value, end) <= 0;
    }

    private static bool Contains(object? target, object? needle)
    {
        if (target is null || needle is null)
        {
            return false;
        }

        string haystack = target.ToString() ?? string.Empty;
        string search = needle.ToString() ?? string.Empty;
        return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryConvertToDouble(object? value, out double number)
    {
        if (value is null)
        {
            number = 0;
            return false;
        }

        if (value is double typedDouble)
        {
            number = typedDouble;
            return true;
        }

        if (value is float typedFloat)
        {
            number = typedFloat;
            return true;
        }

        if (value is decimal typedDecimal)
        {
            number = (double)typedDecimal;
            return true;
        }

        if (value is int or long or short or byte)
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }

        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}

internal static class FeelExpressionParser
{
    public static FeelSyntaxNode Parse(string expression)
    {
        Parser parser = new(expression);
        FeelSyntaxNode result = parser.ParseExpression();
        parser.Expect(FeelTokenKind.End);
        return result;
    }

    private sealed class Parser
    {
        private readonly List<FeelToken> _tokens;
        private int _position;

        public Parser(string expression)
        {
            _tokens = Tokenize(expression);
        }

        public FeelSyntaxNode ParseExpression()
        {
            return ParseOr();
        }

        public void Expect(FeelTokenKind kind)
        {
            if (!Match(kind))
            {
                throw new InvalidOperationException($"Expected token '{kind}'.");
            }
        }

        private FeelSyntaxNode ParseOr()
        {
            FeelSyntaxNode node = ParseAnd();
            while (Match(FeelTokenKind.Or))
            {
                node = new FeelBinaryNode(node, ParseAnd(), FeelBinaryOperator.Or);
            }

            return node;
        }

        private FeelSyntaxNode ParseAnd()
        {
            FeelSyntaxNode node = ParseUnary();
            while (Match(FeelTokenKind.And))
            {
                node = new FeelBinaryNode(node, ParseUnary(), FeelBinaryOperator.And);
            }

            return node;
        }

        private FeelSyntaxNode ParseUnary()
        {
            if (Match(FeelTokenKind.Not))
            {
                return new FeelUnaryNode(ParseUnary(), FeelUnaryOperator.Not);
            }

            return ParseComparison();
        }

        private FeelSyntaxNode ParseComparison()
        {
            FeelSyntaxNode left = ParsePrimary();
            if (Match(FeelTokenKind.Equal))
            {
                return new FeelBinaryNode(left, ParsePrimary(), FeelBinaryOperator.Equal);
            }

            if (Match(FeelTokenKind.NotEqual))
            {
                return new FeelBinaryNode(left, ParsePrimary(), FeelBinaryOperator.NotEqual);
            }

            if (Match(FeelTokenKind.Greater))
            {
                return new FeelBinaryNode(left, ParsePrimary(), FeelBinaryOperator.GreaterThan);
            }

            if (Match(FeelTokenKind.GreaterOrEqual))
            {
                return new FeelBinaryNode(left, ParsePrimary(), FeelBinaryOperator.GreaterThanOrEqual);
            }

            if (Match(FeelTokenKind.Less))
            {
                return new FeelBinaryNode(left, ParsePrimary(), FeelBinaryOperator.LessThan);
            }

            if (Match(FeelTokenKind.LessOrEqual))
            {
                return new FeelBinaryNode(left, ParsePrimary(), FeelBinaryOperator.LessThanOrEqual);
            }

            if (Match(FeelTokenKind.In))
            {
                return ParseMembership(left);
            }

            return left;
        }

        private FeelSyntaxNode ParseMembership(FeelSyntaxNode left)
        {
            if (Match(FeelTokenKind.LeftParen))
            {
                List<FeelSyntaxNode> values = [ParseExpression()];
                while (Match(FeelTokenKind.Comma))
                {
                    values.Add(ParseExpression());
                }

                Expect(FeelTokenKind.RightParen);
                return new FeelInValuesNode(left, values);
            }

            if (Match(FeelTokenKind.LeftBracket))
            {
                FeelSyntaxNode start = ParseExpression();
                if (Match(FeelTokenKind.DotDot))
                {
                    FeelSyntaxNode end = ParseExpression();
                    Expect(FeelTokenKind.RightBracket);
                    return new FeelRangeNode(left, start, end);
                }

                List<FeelSyntaxNode> values = [start];
                while (Match(FeelTokenKind.Comma))
                {
                    values.Add(ParseExpression());
                }

                Expect(FeelTokenKind.RightBracket);
                return new FeelInValuesNode(left, values);
            }

            return new FeelInValuesNode(left, [ParsePrimary()]);
        }

        private FeelSyntaxNode ParsePrimary()
        {
            if (Match(FeelTokenKind.LeftParen))
            {
                FeelSyntaxNode nested = ParseExpression();
                Expect(FeelTokenKind.RightParen);
                return nested;
            }

            if (Current.Kind == FeelTokenKind.Number)
            {
                string raw = Current.Text;
                Next();
                return new FeelNumberLiteralNode(double.Parse(raw, CultureInfo.InvariantCulture));
            }

            if (Current.Kind == FeelTokenKind.String)
            {
                string raw = Current.Text;
                Next();
                return new FeelStringLiteralNode(raw);
            }

            if (Match(FeelTokenKind.True))
            {
                return new FeelBooleanLiteralNode(true);
            }

            if (Match(FeelTokenKind.False))
            {
                return new FeelBooleanLiteralNode(false);
            }

            if (Match(FeelTokenKind.Null))
            {
                return new FeelNullLiteralNode();
            }

            if (Current.Kind == FeelTokenKind.Identifier)
            {
                string identifier = Current.Text;
                Next();
                if (Match(FeelTokenKind.LeftParen))
                {
                    List<FeelSyntaxNode> arguments = [];
                    if (!Match(FeelTokenKind.RightParen))
                    {
                        arguments.Add(ParseExpression());
                        while (Match(FeelTokenKind.Comma))
                        {
                            arguments.Add(ParseExpression());
                        }

                        Expect(FeelTokenKind.RightParen);
                    }

                    return new FeelFunctionCallNode(identifier, arguments);
                }

                return new FeelVariableNode(identifier);
            }

            throw new InvalidOperationException($"Unexpected token '{Current.Text}'.");
        }

        private FeelToken Current => _tokens[_position];

        private bool Match(FeelTokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            _position++;
            return true;
        }

        private void Next()
        {
            if (_position < _tokens.Count - 1)
            {
                _position++;
            }
        }
    }

    private static List<FeelToken> Tokenize(string expression)
    {
        List<FeelToken> tokens = [];
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
                    throw new InvalidOperationException("Unterminated string literal.");
                }

                tokens.Add(new FeelToken(FeelTokenKind.String, expression[start..index]));
                index++;
                continue;
            }

            if (char.IsDigit(current) || (current == '.' && index + 1 < expression.Length && char.IsDigit(expression[index + 1])))
            {
                int start = index;
                index++;
                while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
                {
                    if (index + 1 < expression.Length && expression[index] == '.' && expression[index + 1] == '.')
                    {
                        break;
                    }

                    index++;
                }

                tokens.Add(new FeelToken(FeelTokenKind.Number, expression[start..index]));
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                int start = index;
                index++;
                while (index < expression.Length && (char.IsLetterOrDigit(expression[index]) || expression[index] is '_' or '.'))
                {
                    index++;
                }

                string text = expression[start..index];
                string upper = text.ToUpperInvariant();
                FeelTokenKind kind = upper switch
                {
                    "AND" => FeelTokenKind.And,
                    "OR" => FeelTokenKind.Or,
                    "NOT" => FeelTokenKind.Not,
                    "IN" => FeelTokenKind.In,
                    "TRUE" => FeelTokenKind.True,
                    "FALSE" => FeelTokenKind.False,
                    "NULL" => FeelTokenKind.Null,
                    _ => FeelTokenKind.Identifier
                };
                tokens.Add(new FeelToken(kind, text));
                continue;
            }

            if (current == '.' && index + 1 < expression.Length && expression[index + 1] == '.')
            {
                tokens.Add(new FeelToken(FeelTokenKind.DotDot, ".."));
                index += 2;
                continue;
            }

            if (current == '!' && index + 1 < expression.Length && expression[index + 1] == '=')
            {
                tokens.Add(new FeelToken(FeelTokenKind.NotEqual, "!="));
                index += 2;
                continue;
            }

            if (current == '>' && index + 1 < expression.Length && expression[index + 1] == '=')
            {
                tokens.Add(new FeelToken(FeelTokenKind.GreaterOrEqual, ">="));
                index += 2;
                continue;
            }

            if (current == '<' && index + 1 < expression.Length && expression[index + 1] == '=')
            {
                tokens.Add(new FeelToken(FeelTokenKind.LessOrEqual, "<="));
                index += 2;
                continue;
            }

            tokens.Add(current switch
            {
                '(' => new FeelToken(FeelTokenKind.LeftParen, "("),
                ')' => new FeelToken(FeelTokenKind.RightParen, ")"),
                '[' => new FeelToken(FeelTokenKind.LeftBracket, "["),
                ']' => new FeelToken(FeelTokenKind.RightBracket, "]"),
                ',' => new FeelToken(FeelTokenKind.Comma, ","),
                '=' => new FeelToken(FeelTokenKind.Equal, "="),
                '>' => new FeelToken(FeelTokenKind.Greater, ">"),
                '<' => new FeelToken(FeelTokenKind.Less, "<"),
                _ => throw new InvalidOperationException($"Unsupported token '{current}'.")
            });
            index++;
        }

        tokens.Add(new FeelToken(FeelTokenKind.End, string.Empty));
        return tokens;
    }
}

internal abstract record FeelSyntaxNode;

internal sealed record FeelBooleanLiteralNode(bool Value) : FeelSyntaxNode;

internal sealed record FeelNumberLiteralNode(double Value) : FeelSyntaxNode;

internal sealed record FeelStringLiteralNode(string Value) : FeelSyntaxNode;

internal sealed record FeelNullLiteralNode() : FeelSyntaxNode;

internal sealed record FeelVariableNode(string Path) : FeelSyntaxNode;

internal sealed record FeelFunctionCallNode(string Name, IReadOnlyList<FeelSyntaxNode> Arguments) : FeelSyntaxNode;

internal sealed record FeelUnaryNode(FeelSyntaxNode Operand, FeelUnaryOperator Operator) : FeelSyntaxNode;

internal sealed record FeelBinaryNode(FeelSyntaxNode Left, FeelSyntaxNode Right, FeelBinaryOperator Operator) : FeelSyntaxNode;

internal sealed record FeelInValuesNode(FeelSyntaxNode Left, IReadOnlyList<FeelSyntaxNode> Values) : FeelSyntaxNode;

internal sealed record FeelRangeNode(FeelSyntaxNode Left, FeelSyntaxNode Start, FeelSyntaxNode End) : FeelSyntaxNode;

internal readonly record struct FeelToken(FeelTokenKind Kind, string Text);

internal enum FeelUnaryOperator
{
    Not
}

internal enum FeelBinaryOperator
{
    And,
    Or,
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

internal enum FeelTokenKind
{
    Identifier,
    Number,
    String,
    True,
    False,
    Null,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Comma,
    DotDot,
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    And,
    Or,
    Not,
    In,
    End
}
