using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Muonroi.RuleEngine.Runtime.Compilation.Feel;

/// <summary>
/// Converts parsed FEEL syntax nodes into expression trees backed by runtime helpers.
/// Supports: logical ops, comparisons, arithmetic, if/then/else, between,
/// some/every quantifiers, for loops, not(), and function calls.
/// </summary>
internal static class ExpressionTreeVisitor
{
    private static readonly MethodInfo ResolveValueMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(ResolveValue), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ToBooleanMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(ToBoolean), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo CompareMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(Compare), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo InValuesMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(InValues), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo InRangeMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(InRange), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ContainsMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(Contains), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ArithmeticMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(Arithmetic), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo SomeMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(SomeInList), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo EveryMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(EveryInList), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ForMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(ForInList), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo BetweenMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(BetweenCheck), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Expression VisitAsBoolean(FeelSyntaxNode node, ParameterExpression varsParameter)
    {
        return node switch
        {
            FeelUnaryNode unary when unary.Operator == FeelUnaryOperator.Not
                => Expression.Not(VisitAsBoolean(unary.Operand, varsParameter)),
            FeelUnaryNode unary when unary.Operator == FeelUnaryOperator.Negate
                => Expression.Call(ToBooleanMethod, VisitAsObject(node, varsParameter)),
            FeelBinaryNode binary when binary.Operator == FeelBinaryOperator.And
                => Expression.AndAlso(VisitAsBoolean(binary.Left, varsParameter), VisitAsBoolean(binary.Right, varsParameter)),
            FeelBinaryNode binary when binary.Operator == FeelBinaryOperator.Or
                => Expression.OrElse(VisitAsBoolean(binary.Left, varsParameter), VisitAsBoolean(binary.Right, varsParameter)),
            FeelBinaryNode binary when binary.Operator is FeelBinaryOperator.Equal
                or FeelBinaryOperator.NotEqual
                or FeelBinaryOperator.GreaterThan
                or FeelBinaryOperator.GreaterThanOrEqual
                or FeelBinaryOperator.LessThan
                or FeelBinaryOperator.LessThanOrEqual
                => BuildComparison(binary, varsParameter),
            FeelInValuesNode valuesNode
                => Expression.Call(InValuesMethod, VisitAsObject(valuesNode.Left, varsParameter), BuildValuesArray(valuesNode.Values, varsParameter)),
            FeelRangeNode rangeNode
                => Expression.Call(InRangeMethod, VisitAsObject(rangeNode.Left, varsParameter), VisitAsObject(rangeNode.Start, varsParameter), VisitAsObject(rangeNode.End, varsParameter)),
            FeelIfNode ifNode
                => Expression.Condition(
                    VisitAsBoolean(ifNode.Condition, varsParameter),
                    Expression.Call(ToBooleanMethod, VisitAsObject(ifNode.ThenBranch, varsParameter)),
                    Expression.Call(ToBooleanMethod, VisitAsObject(ifNode.ElseBranch, varsParameter))),
            FeelBetweenNode betweenNode
                => Expression.Call(BetweenMethod,
                    VisitAsObject(betweenNode.Value, varsParameter),
                    VisitAsObject(betweenNode.Low, varsParameter),
                    VisitAsObject(betweenNode.High, varsParameter)),
            FeelSomeNode someNode
                => Expression.Call(SomeMethod,
                    varsParameter,
                    Expression.Constant(someNode.Variable),
                    VisitAsObject(someNode.List, varsParameter),
                    Expression.Constant(someNode.Predicate)),
            FeelEveryNode everyNode
                => Expression.Call(EveryMethod,
                    varsParameter,
                    Expression.Constant(everyNode.Variable),
                    VisitAsObject(everyNode.List, varsParameter),
                    Expression.Constant(everyNode.Predicate)),
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
            FeelFunctionCallNode functionNode when string.Equals(functionNode.Name, "not", StringComparison.OrdinalIgnoreCase) && functionNode.Arguments.Count == 1
                => Expression.Convert(Expression.Not(VisitAsBoolean(functionNode.Arguments[0], varsParameter)), typeof(object)),
            FeelUnaryNode unaryNode when unaryNode.Operator == FeelUnaryOperator.Not
                => Expression.Convert(VisitAsBoolean(unaryNode, varsParameter), typeof(object)),
            FeelUnaryNode unaryNode when unaryNode.Operator == FeelUnaryOperator.Negate
                => Expression.Call(ArithmeticMethod,
                    Expression.Constant((double)0, typeof(object)),
                    VisitAsObject(unaryNode.Operand, varsParameter),
                    Expression.Constant("-")),
            FeelBinaryNode binaryNode when binaryNode.Operator is FeelBinaryOperator.And or FeelBinaryOperator.Or
                => Expression.Convert(VisitAsBoolean(binaryNode, varsParameter), typeof(object)),
            FeelBinaryNode binaryNode when binaryNode.Operator is FeelBinaryOperator.Equal
                or FeelBinaryOperator.NotEqual
                or FeelBinaryOperator.GreaterThan
                or FeelBinaryOperator.GreaterThanOrEqual
                or FeelBinaryOperator.LessThan
                or FeelBinaryOperator.LessThanOrEqual
                => Expression.Convert(BuildComparison(binaryNode, varsParameter), typeof(object)),
            FeelBinaryNode binaryNode when binaryNode.Operator is FeelBinaryOperator.Add
                or FeelBinaryOperator.Subtract
                or FeelBinaryOperator.Multiply
                or FeelBinaryOperator.Divide
                => BuildArithmetic(binaryNode, varsParameter),
            FeelInValuesNode inNode => Expression.Convert(VisitAsBoolean(inNode, varsParameter), typeof(object)),
            FeelRangeNode rangeNode => Expression.Convert(VisitAsBoolean(rangeNode, varsParameter), typeof(object)),
            FeelIfNode ifNode
                => Expression.Condition(
                    VisitAsBoolean(ifNode.Condition, varsParameter),
                    VisitAsObject(ifNode.ThenBranch, varsParameter),
                    VisitAsObject(ifNode.ElseBranch, varsParameter)),
            FeelBetweenNode betweenNode
                => Expression.Convert(
                    Expression.Call(BetweenMethod,
                        VisitAsObject(betweenNode.Value, varsParameter),
                        VisitAsObject(betweenNode.Low, varsParameter),
                        VisitAsObject(betweenNode.High, varsParameter)),
                    typeof(object)),
            FeelSomeNode someNode
                => Expression.Convert(
                    Expression.Call(SomeMethod, varsParameter,
                        Expression.Constant(someNode.Variable),
                        VisitAsObject(someNode.List, varsParameter),
                        Expression.Constant(someNode.Predicate)),
                    typeof(object)),
            FeelEveryNode everyNode
                => Expression.Convert(
                    Expression.Call(EveryMethod, varsParameter,
                        Expression.Constant(everyNode.Variable),
                        VisitAsObject(everyNode.List, varsParameter),
                        Expression.Constant(everyNode.Predicate)),
                    typeof(object)),
            FeelForNode forNode
                => Expression.Call(ForMethod, varsParameter,
                    Expression.Constant(forNode.Variable),
                    VisitAsObject(forNode.List, varsParameter),
                    Expression.Constant(forNode.Body)),
            FeelListLiteralNode listNode => BuildListLiteral(listNode, varsParameter),
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

    private static Expression BuildArithmetic(FeelBinaryNode node, ParameterExpression varsParameter)
    {
        string op = node.Operator switch
        {
            FeelBinaryOperator.Add => "+",
            FeelBinaryOperator.Subtract => "-",
            FeelBinaryOperator.Multiply => "*",
            FeelBinaryOperator.Divide => "/",
            _ => throw new NotSupportedException($"Unsupported arithmetic operator '{node.Operator}'.")
        };
        return Expression.Call(ArithmeticMethod,
            VisitAsObject(node.Left, varsParameter),
            VisitAsObject(node.Right, varsParameter),
            Expression.Constant(op));
    }

    private static NewArrayExpression BuildValuesArray(IReadOnlyList<FeelSyntaxNode> values, ParameterExpression varsParameter)
    {
        return Expression.NewArrayInit(typeof(object), values.Select(value => Expression.Convert(VisitAsObject(value, varsParameter), typeof(object))));
    }

    private static Expression BuildListLiteral(FeelListLiteralNode listNode, ParameterExpression varsParameter)
    {
        MethodInfo toListMethod = typeof(ExpressionTreeVisitor).GetMethod(nameof(CreateList), BindingFlags.NonPublic | BindingFlags.Static)!;
        return Expression.Call(toListMethod, BuildValuesArray(listNode.Items, varsParameter));
    }

    // --- Runtime helpers ---

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
        if (left is null && right is null) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        if (left is DateTime leftDateTime && right is DateTime rightDateTime)
            return leftDateTime.CompareTo(rightDateTime);

        if (left is bool leftBool && right is bool rightBool)
            return leftBool.CompareTo(rightBool);

        if (TryConvertToDouble(left, out double leftNumber) && TryConvertToDouble(right, out double rightNumber))
        {
            double delta = leftNumber - rightNumber;
            if (Math.Abs(delta) <= double.Epsilon) return 0;
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
        if (target is null || needle is null) return false;
        string haystack = target.ToString() ?? string.Empty;
        string search = needle.ToString() ?? string.Empty;
        return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static object? Arithmetic(object? left, object? right, string op)
    {
        if (!TryConvertToDouble(left, out double l) || !TryConvertToDouble(right, out double r))
        {
            if (op == "+" && (left is string || right is string))
            {
                return $"{left}{right}";
            }
            throw new InvalidOperationException($"Cannot perform '{op}' on non-numeric values.");
        }

        return op switch
        {
            "+" => (object)(l + r),
            "-" => (object)(l - r),
            "*" => (object)(l * r),
            "/" => r == 0 ? throw new DivideByZeroException("Division by zero in FEEL expression.") : (object)(l / r),
            _ => throw new NotSupportedException($"Unsupported arithmetic operator '{op}'.")
        };
    }

    private static bool BetweenCheck(object? value, object? low, object? high)
    {
        return Compare(value, low) >= 0 && Compare(value, high) <= 0;
    }

    private static bool SomeInList(IDictionary<string, object> vars, string variable, object? list, string predicateExpression)
    {
        IEnumerable<object?> items = CoerceToList(list);
        foreach (object? item in items)
        {
            Dictionary<string, object> scopedVars = new(vars, StringComparer.OrdinalIgnoreCase);
            if (item is not null) scopedVars[variable] = item;
            bool result = Muonroi.RuleEngine.DecisionTable.Feel.FeelEvaluator.Evaluate(predicateExpression, scopedVars);
            if (result) return true;
        }
        return false;
    }

    private static bool EveryInList(IDictionary<string, object> vars, string variable, object? list, string predicateExpression)
    {
        IEnumerable<object?> items = CoerceToList(list);
        foreach (object? item in items)
        {
            Dictionary<string, object> scopedVars = new(vars, StringComparer.OrdinalIgnoreCase);
            if (item is not null) scopedVars[variable] = item;
            bool result = Muonroi.RuleEngine.DecisionTable.Feel.FeelEvaluator.Evaluate(predicateExpression, scopedVars);
            if (!result) return false;
        }
        return true;
    }

    private static object? ForInList(IDictionary<string, object> vars, string variable, object? list, string bodyExpression)
    {
        IEnumerable<object?> items = CoerceToList(list);
        List<object?> results = [];
        foreach (object? item in items)
        {
            Dictionary<string, object> scopedVars = new(vars, StringComparer.OrdinalIgnoreCase);
            if (item is not null) scopedVars[variable] = item;
            object? result = Muonroi.RuleEngine.DecisionTable.Feel.FeelEvaluator.EvaluateValue(bodyExpression, scopedVars);
            results.Add(result);
        }
        return results;
    }

    private static object CreateList(object?[] items)
    {
        return new List<object?>(items);
    }

    private static IEnumerable<object?> CoerceToList(object? value)
    {
        if (value is IEnumerable<object?> enumerable) return enumerable;
        if (value is System.Collections.IEnumerable ie and not string)
        {
            List<object?> list = [];
            foreach (object? item in ie) list.Add(item);
            return list;
        }
        return value is null ? [] : [value];
    }

    private static bool TryConvertToDouble(object? value, out double number)
    {
        if (value is null) { number = 0; return false; }
        if (value is double typedDouble) { number = typedDouble; return true; }
        if (value is float typedFloat) { number = typedFloat; return true; }
        if (value is decimal typedDecimal) { number = (double)typedDecimal; return true; }
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
            // if/then/else
            if (Match(FeelTokenKind.If))
            {
                FeelSyntaxNode condition = ParseExpression();
                Expect(FeelTokenKind.Then);
                FeelSyntaxNode thenBranch = ParseExpression();
                Expect(FeelTokenKind.Else);
                FeelSyntaxNode elseBranch = ParseExpression();
                return new FeelIfNode(condition, thenBranch, elseBranch);
            }

            // for x in list return expr
            if (Current.Kind == FeelTokenKind.For)
            {
                return ParseForExpression();
            }

            // some x in list satisfies pred
            if (Current.Kind == FeelTokenKind.Some)
            {
                return ParseSomeExpression();
            }

            // every x in list satisfies pred
            if (Current.Kind == FeelTokenKind.Every)
            {
                return ParseEveryExpression();
            }

            return ParseOr();
        }

        public void Expect(FeelTokenKind kind)
        {
            if (!Match(kind))
            {
                throw new InvalidOperationException($"Expected token '{kind}' but found '{Current.Kind}'.");
            }
        }

        private FeelSyntaxNode ParseForExpression()
        {
            Expect(FeelTokenKind.For);
            string variable = Current.Text;
            Expect(FeelTokenKind.Identifier);
            Expect(FeelTokenKind.In);
            FeelSyntaxNode list = ParseOr();
            Expect(FeelTokenKind.Return);
            // Capture the rest as a string expression for runtime evaluation
            string bodyExpr = CaptureRemainingExpression();
            return new FeelForNode(variable, list, bodyExpr);
        }

        private FeelSyntaxNode ParseSomeExpression()
        {
            Expect(FeelTokenKind.Some);
            string variable = Current.Text;
            Expect(FeelTokenKind.Identifier);
            Expect(FeelTokenKind.In);
            FeelSyntaxNode list = ParseOr();
            Expect(FeelTokenKind.Satisfies);
            string predicateExpr = CaptureRemainingExpression();
            return new FeelSomeNode(variable, list, predicateExpr);
        }

        private FeelSyntaxNode ParseEveryExpression()
        {
            Expect(FeelTokenKind.Every);
            string variable = Current.Text;
            Expect(FeelTokenKind.Identifier);
            Expect(FeelTokenKind.In);
            FeelSyntaxNode list = ParseOr();
            Expect(FeelTokenKind.Satisfies);
            string predicateExpr = CaptureRemainingExpression();
            return new FeelEveryNode(variable, list, predicateExpr);
        }

        private string CaptureRemainingExpression()
        {
            // Capture all remaining tokens (excluding End) as a string
            List<string> parts = [];
            while (Current.Kind != FeelTokenKind.End)
            {
                parts.Add(Current.Text);
                Next();
            }
            return string.Join(" ", parts);
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
            FeelSyntaxNode left = ParseAdditive();

            if (Match(FeelTokenKind.Equal))
                return new FeelBinaryNode(left, ParseAdditive(), FeelBinaryOperator.Equal);
            if (Match(FeelTokenKind.NotEqual))
                return new FeelBinaryNode(left, ParseAdditive(), FeelBinaryOperator.NotEqual);
            if (Match(FeelTokenKind.Greater))
                return new FeelBinaryNode(left, ParseAdditive(), FeelBinaryOperator.GreaterThan);
            if (Match(FeelTokenKind.GreaterOrEqual))
                return new FeelBinaryNode(left, ParseAdditive(), FeelBinaryOperator.GreaterThanOrEqual);
            if (Match(FeelTokenKind.Less))
                return new FeelBinaryNode(left, ParseAdditive(), FeelBinaryOperator.LessThan);
            if (Match(FeelTokenKind.LessOrEqual))
                return new FeelBinaryNode(left, ParseAdditive(), FeelBinaryOperator.LessThanOrEqual);
            if (Match(FeelTokenKind.In))
                return ParseMembership(left);

            // between X and Y
            if (Match(FeelTokenKind.Between))
            {
                FeelSyntaxNode low = ParseAdditive();
                Expect(FeelTokenKind.And);
                FeelSyntaxNode high = ParseAdditive();
                return new FeelBetweenNode(left, low, high);
            }

            return left;
        }

        private FeelSyntaxNode ParseAdditive()
        {
            FeelSyntaxNode left = ParseMultiplicative();
            while (true)
            {
                if (Match(FeelTokenKind.Plus))
                {
                    left = new FeelBinaryNode(left, ParseMultiplicative(), FeelBinaryOperator.Add);
                }
                else if (Match(FeelTokenKind.Minus))
                {
                    left = new FeelBinaryNode(left, ParseMultiplicative(), FeelBinaryOperator.Subtract);
                }
                else
                {
                    break;
                }
            }
            return left;
        }

        private FeelSyntaxNode ParseMultiplicative()
        {
            FeelSyntaxNode left = ParseUnaryPrefix();
            while (true)
            {
                if (Match(FeelTokenKind.Star))
                {
                    left = new FeelBinaryNode(left, ParseUnaryPrefix(), FeelBinaryOperator.Multiply);
                }
                else if (Match(FeelTokenKind.Slash))
                {
                    left = new FeelBinaryNode(left, ParseUnaryPrefix(), FeelBinaryOperator.Divide);
                }
                else
                {
                    break;
                }
            }
            return left;
        }

        private FeelSyntaxNode ParseUnaryPrefix()
        {
            if (Match(FeelTokenKind.Minus))
            {
                return new FeelUnaryNode(ParsePrimary(), FeelUnaryOperator.Negate);
            }
            return ParsePrimary();
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

            // List literal: [1, 2, 3]
            if (Match(FeelTokenKind.LeftBracket))
            {
                List<FeelSyntaxNode> items = [];
                if (!Match(FeelTokenKind.RightBracket))
                {
                    items.Add(ParseExpression());
                    while (Match(FeelTokenKind.Comma))
                    {
                        items.Add(ParseExpression());
                    }
                    Expect(FeelTokenKind.RightBracket);
                }
                return new FeelListLiteralNode(items);
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
                return new FeelBooleanLiteralNode(true);

            if (Match(FeelTokenKind.False))
                return new FeelBooleanLiteralNode(false);

            if (Match(FeelTokenKind.Null))
                return new FeelNullLiteralNode();

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
            if (Current.Kind != kind) return false;
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
                    "IF" => FeelTokenKind.If,
                    "THEN" => FeelTokenKind.Then,
                    "ELSE" => FeelTokenKind.Else,
                    "BETWEEN" => FeelTokenKind.Between,
                    "SOME" => FeelTokenKind.Some,
                    "EVERY" => FeelTokenKind.Every,
                    "SATISFIES" => FeelTokenKind.Satisfies,
                    "FOR" => FeelTokenKind.For,
                    "RETURN" => FeelTokenKind.Return,
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
                '+' => new FeelToken(FeelTokenKind.Plus, "+"),
                '-' => new FeelToken(FeelTokenKind.Minus, "-"),
                '*' => new FeelToken(FeelTokenKind.Star, "*"),
                '/' => new FeelToken(FeelTokenKind.Slash, "/"),
                _ => throw new InvalidOperationException($"Unsupported token '{current}'.")
            });
            index++;
        }

        tokens.Add(new FeelToken(FeelTokenKind.End, string.Empty));
        return tokens;
    }
}

// --- AST Nodes ---

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
internal sealed record FeelListLiteralNode(IReadOnlyList<FeelSyntaxNode> Items) : FeelSyntaxNode;

// New nodes for compiler completion
internal sealed record FeelIfNode(FeelSyntaxNode Condition, FeelSyntaxNode ThenBranch, FeelSyntaxNode ElseBranch) : FeelSyntaxNode;
internal sealed record FeelBetweenNode(FeelSyntaxNode Value, FeelSyntaxNode Low, FeelSyntaxNode High) : FeelSyntaxNode;
internal sealed record FeelSomeNode(string Variable, FeelSyntaxNode List, string Predicate) : FeelSyntaxNode;
internal sealed record FeelEveryNode(string Variable, FeelSyntaxNode List, string Predicate) : FeelSyntaxNode;
internal sealed record FeelForNode(string Variable, FeelSyntaxNode List, string Body) : FeelSyntaxNode;

internal readonly record struct FeelToken(FeelTokenKind Kind, string Text);

internal enum FeelUnaryOperator
{
    Not,
    Negate
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
    LessThanOrEqual,
    Add,
    Subtract,
    Multiply,
    Divide
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
    End,
    // New tokens
    Plus,
    Minus,
    Star,
    Slash,
    If,
    Then,
    Else,
    Between,
    Some,
    Every,
    Satisfies,
    For,
    Return
}
