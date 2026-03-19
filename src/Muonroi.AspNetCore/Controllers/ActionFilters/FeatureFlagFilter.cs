namespace Muonroi.AspNetCore.Controllers.ActionFilters;

/// <summary>
/// Attribute used to enable or disable an endpoint based on a configured feature flag.
/// </summary>
/// <remarks>
/// This attribute applies a <see cref="FeatureFlagFilter"/> to the target controller
/// or action. The filter checks whether the specified feature flag is enabled
/// in the application configuration.
///
/// Feature flags are resolved from the configuration section:
/// <c>Features:{featureName}</c>.
///
/// If the feature is disabled, the endpoint returns <see cref="StatusCodes.Status404NotFound"/>.
/// This behavior helps hide unavailable features from clients.
/// </remarks>
/// <example>
/// Configuration:
/// <code>
/// "Features": {
///     "NewDashboard": true
/// }
/// </code>
///
/// Usage:
/// <code>
/// [FeatureFlag("NewDashboard")]
/// public IActionResult GetDashboard()
/// {
///     ...
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class FeatureFlagAttribute : TypeFilterAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagAttribute"/> class.
    /// </summary>
    /// <param name="feature">
    /// The name of the feature flag that controls access to the endpoint.
    /// </param>
    /// <remarks>
    /// The feature flag value is resolved from the configuration path:
    /// <c>Features:{feature}</c>.
    /// </remarks>
    public FeatureFlagAttribute(string feature) : base(typeof(FeatureFlagFilter))
    {
        Arguments = [feature];
    }
}

/// <summary>
/// Action filter that enforces feature flag validation for a request.
/// </summary>
/// <remarks>
/// The filter checks whether a specific feature flag is enabled in the application's
/// configuration. If the feature is disabled, the request pipeline is short-circuited
/// and a <see cref="StatusCodes.Status404NotFound"/> response is returned.
///
/// This filter is typically used together with <see cref="FeatureFlagAttribute"/>
/// to implement feature toggles and progressive feature rollouts.
/// </remarks>
public sealed class FeatureFlagFilter(string feature, IConfiguration configuration) : IAsyncActionFilter
{
    /// <summary>
    /// Executes the action filter asynchronously to determine whether the feature
    /// associated with the current endpoint is enabled.
    /// </summary>
    /// <param name="context">
    /// The <see cref="ActionExecutingContext"/> for the current request.
    /// </param>
    /// <param name="next">
    /// The delegate that executes the next action filter or action method
    /// when the feature is enabled.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous filter operation.
    /// </returns>
    /// <remarks>
    /// The feature state is resolved from the configuration key:
    /// <c>Features:{feature}</c>.
    ///
    /// When the feature is disabled, the request pipeline is terminated
    /// with a 404 response to avoid exposing unavailable functionality.
    /// </remarks>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        bool enabled = configuration.GetValue<bool>($"Features:{feature}");
        if (!enabled)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status404NotFound);
            return;
        }

        await next();
    }
}
