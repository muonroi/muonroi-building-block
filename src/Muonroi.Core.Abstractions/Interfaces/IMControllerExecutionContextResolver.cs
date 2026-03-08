using Microsoft.AspNetCore.Http;
using Muonroi.Core.Abstractions.Models;

namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Resolves the execution context for a controller.
/// </summary>
public interface IMControllerExecutionContextResolver
{
    /// <summary>
    /// Resolves the controller execution context from the current HTTP context.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The resolved controller execution context.</returns>
    MControllerExecutionContext Resolve(HttpContext httpContext);
}
