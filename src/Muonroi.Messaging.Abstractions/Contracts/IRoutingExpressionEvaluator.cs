using System;
using System.Collections.Generic;

namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Evaluates expressions for dynamic message routing.
/// </summary>
public interface IRoutingExpressionEvaluator
{
    /// <summary>
    /// Compiles an expression into an evaluable predicate.
    /// </summary>
    /// <param name="expression">The routing expression (e.g., FEEL).</param>
    /// <returns>A function that evaluates the expression against a dictionary of variables.</returns>
    Func<IDictionary<string, object>, bool> Compile(string expression);
}
