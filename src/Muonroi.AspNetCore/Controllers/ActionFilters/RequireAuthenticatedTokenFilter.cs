namespace Muonroi.AspNetCore.Controllers.ActionFilters;

/// <inheritdoc />
public class RequireAuthenticatedTokenFilter<TDbContext, TPermission>(
    TDbContext dbContext,
    IMultiLevelCacheService cacheService,
    IMLog<MDbContext> logger)
    : IAsyncActionFilter
    where TDbContext : MDbContext
    where TPermission : Enum
{
/// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();
        if (endpoint is null)
        {
            await next();
            return;
        }

        if (endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() is not null)
        {
            await next();
            return;
        }

        if (endpoint.Metadata.GetMetadata<AuthorizeAttribute>() is null)
        {
            await next();
            return;
        }

        MRefreshToken? refresh =
            await dbContext.ResolveTokenFromHttpContext(context.HttpContext, cacheService, logger: logger
                );
        if (refresh is null)
        {
            if (string.IsNullOrEmpty(context.HttpContext.Request.Headers.Authorization.ToString()))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            context.HttpContext.User = new ClaimsPrincipal();
            context.HttpContext.Items.Clear();
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
