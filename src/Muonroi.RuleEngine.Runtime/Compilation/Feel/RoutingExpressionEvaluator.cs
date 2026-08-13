using System;
using System.Collections.Generic;
using Muonroi.Messaging.Abstractions.Contracts;

namespace Muonroi.RuleEngine.Runtime.Compilation.Feel;

/// <summary>
/// Implements the routing expression evaluator using the FEEL compiler.
/// </summary>
public class RoutingExpressionEvaluator : IRoutingExpressionEvaluator
{
    /// <inheritdoc/>
    public Func<IDictionary<string, object>, bool> Compile(string expression)
    {
        return FeelExpressionCompiler.Compile(expression);
    }
}
