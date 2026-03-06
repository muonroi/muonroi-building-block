namespace Muonroi.AspNetCore.Controllers.ActionFilters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class FeatureFlagAttribute : TypeFilterAttribute
{
    public FeatureFlagAttribute(string feature) : base(typeof(FeatureFlagFilter))
    {
        Arguments = [feature];
    }
}

public sealed class FeatureFlagFilter(string feature, IConfiguration configuration) : IAsyncActionFilter
{
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
