namespace Muonroi.AspNetCore.Controllers.ActionFilters;

public class RequestLoggingFilter(
    IMLog<RequestLoggingFilter>? logger,
    IMJsonSerializeService jsonSerialize,
    MAuthenticateInfoContext authContext,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogSanitizer? logSanitizer = null) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        HttpRequest request = context.HttpContext.Request;
        Stopwatch watch = Stopwatch.StartNew();
        IDictionary<string, object?> sanitizedArguments = logSanitizer?.Sanitize(context.ActionArguments) ?? context.ActionArguments;

        MLogEntry entry = new()
        {
            TransactionId = context.HttpContext.TraceIdentifier,
            SiteCode = configuration["SiteCode"],
            TenantId = TenantContext.CurrentTenantId,
            LogType = "Info",
            Server = Environment.MachineName,
            Caller = authContext.Caller,
            Client = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            Username = authContext.CurrentUsername,
            Location = context.ActionDescriptor.DisplayName,
            Data = SerializeArgumentsSafe(sanitizedArguments),
            ApplicationInfo = hostEnvironment.ApplicationName,
            Message = $"Incoming {request.Method} {request.Path}"
        };
        logger?.Info("{@Entry}", entry);

        ActionExecutedContext executed = await next();
        watch.Stop();

        entry.Elapsed = watch.ElapsedMilliseconds;
        entry.Message = $"Completed {request.Method} {request.Path}";
        entry.LogType = executed.Exception is null ? "Info" : "Error";
        entry.ErrorCode = executed.Exception?.GetType().Name;
        entry.LogTrace = executed.Exception?.ToString();

        logger?.Info("{@Entry}", entry);
    }

    private string SerializeArgumentsSafe(IDictionary<string, object?> arguments)
    {
        Dictionary<string, string?> serialized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, object? value) in arguments)
        {
            serialized[key] = ConvertArgumentToSafeString(value);
        }

        return jsonSerialize.Serialize(serialized);
    }

    private string? ConvertArgumentToSafeString(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is CancellationToken)
        {
            return "[CancellationToken]";
        }

        if (value is HttpContext or HttpRequest or HttpResponse)
        {
            return $"[{value.GetType().Name}]";
        }

        if (value is System.Security.Claims.ClaimsPrincipal)
        {
            return "[ClaimsPrincipal]";
        }

        if (value is System.IO.Stream)
        {
            return $"[{value.GetType().Name}]";
        }

        if (value is WaitHandle)
        {
            return "[WaitHandle]";
        }

        Type type = value.GetType();
        if (type.IsPrimitive ||
            type.IsEnum ||
            value is string ||
            value is decimal ||
            value is DateTime ||
            value is DateTimeOffset ||
            value is TimeSpan ||
            value is Guid)
        {
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        try
        {
            return jsonSerialize.Serialize(value);
        }
        catch
        {
            return $"[{type.FullName}]";
        }
    }
}
