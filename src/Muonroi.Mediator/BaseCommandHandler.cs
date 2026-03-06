using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Response;
using Muonroi.Core.Extensions;
using Muonroi.Mapper.Interfaces;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator;

public abstract class BaseCommandHandler(
    IMapper mapper,
    IAuthenticateInfoContext tokenInfo,
    ILogger logger,
    IMediator mediator,
    MPaginationConfig? paginationConfig)
{
    protected MPaginationConfig? PaginationConfig => paginationConfig;
    protected IAuthenticateInfoContext TokenInfo => tokenInfo;
    protected int DefaultPageIndex => paginationConfig?.DefaultPageIndex ?? 0;
    protected int DefaultPageSize => paginationConfig?.DefaultPageSize ?? 0;
    protected int MaxPageSize => paginationConfig?.MaxPageSize ?? 0;
    protected IMapper Mapper => mapper;
    protected ILogger Logger => logger;
    protected IMediator Mediator => mediator;
    protected string CurrentUserId => tokenInfo.CurrentUserGuid;
    protected string CurrentUsername => tokenInfo.CurrentUsername;
    protected static double NowTsOnlyDay => DateTime.UtcNow.GetTimeStamp(); // MBB001-exempt: static-class boundary
    protected static double NowTs => DateTime.UtcNow.GetTimeStamp(true); // MBB001-exempt: static-class boundary
    protected static DateTime Now => DateTime.UtcNow; // MBB001-exempt: static-class boundary


    protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    protected async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : Mediator.Interfaces.INotification
    {
        await Mediator.Publish(notification, cancellationToken);
    }

    protected void LogInfo(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        logger.LogInformation(message);
    }

    protected void LogError(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        logger.LogError(message);
    }

    protected void LogError(Exception ex)
    {
        logger.LogError(ex, ex.Message);
    }

    protected void LogError(string message, Exception ex)
    {
        logger.LogError(ex, message);
    }

    protected void LogWarning(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        logger.LogWarning(message);
    }

    protected T Map<T>(object source)
    {
        return Mapper.Map<T>(source);
    }

    protected T Map<T>(object source, T destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        object? mapped = Mapper.Map(source, (object)destination);
        return mapped is null ? throw new InvalidOperationException("Mapping resulted in null.") : (T)mapped;
    }
}
