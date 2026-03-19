using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Extensions;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator;

/// <summary>
/// Canonical base class for all mediator command/query handlers in the Muonroi ecosystem.
/// Uses ecosystem wrappers (IMDateTimeService, ISystemExecutionContextAccessor, IMLog)
/// rather than static ambient state.
/// </summary>
public abstract class MBaseCommandHandler(
    IMapper mapper,
    IAuthenticateInfoContext tokenInfo,
    IMLog<MBaseCommandHandler> logger,
    IMediator mediator,
    ISystemExecutionContextAccessor contextAccessor,
    IMDateTimeService dateTimeService,
    MPaginationConfig? paginationConfig = null)
{
    // ── Pagination ───────────────────────────────────────────────────────────────
    protected MPaginationConfig? PaginationConfig => paginationConfig;
    protected int DefaultPageIndex => paginationConfig?.DefaultPageIndex ?? 0;
    protected int DefaultPageSize => paginationConfig?.DefaultPageSize ?? 0;
    protected int MaxPageSize => paginationConfig?.MaxPageSize ?? 0;

    // ── Auth context (legacy, keep for backward compat) ──────────────────────────
    protected IAuthenticateInfoContext TokenInfo => tokenInfo;
    protected string CurrentUserId => tokenInfo.CurrentUserGuid;
    protected string CurrentUsername => tokenInfo.CurrentUsername;

    // ── Execution context (ecosystem wrapper) ────────────────────────────────────
    protected ISystemExecutionContextAccessor ContextAccessor => contextAccessor;
    protected string? CurrentTenantId => contextAccessor.Get().TenantId;
    protected string? CorrelationId => contextAccessor.Get().CorrelationId;
    protected IReadOnlyList<string> CurrentPermissions => contextAccessor.Get().Permissions;

    // ── Time (ecosystem wrapper) ─────────────────────────────────────────────────
    protected IMDateTimeService DateTimeService => dateTimeService;

    // ── Logging ──────────────────────────────────────────────────────────────────
    protected IMapper Mapper => mapper;
    protected IMLog<MBaseCommandHandler> Logger => logger;
    protected IMediator Mediator => mediator;

    // ── Timestamp helpers (static — MBB001-exempt: static-class boundary) ────────
    protected static double NowTsOnlyDay => DateTime.UtcNow.GetTimeStamp(); // MBB001-exempt: static-class boundary
    protected static double NowTs => DateTime.UtcNow.GetTimeStamp(true);    // MBB001-exempt: static-class boundary
    protected static DateTime Now => DateTime.UtcNow;                        // MBB001-exempt: static-class boundary

    // ── Dispatch helpers ─────────────────────────────────────────────────────────

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

    // ── Logging helpers ───────────────────────────────────────────────────────────

    protected void LogInfo(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        logger?.Info(message);
    }

    protected void LogError(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        logger.LogError(message);
    }

    protected void LogError(Exception ex) => logger.LogError(ex, ex.Message);

    protected void LogError(string message, Exception ex) => logger.LogError(ex, message);

    protected void LogWarning(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        logger.LogWarning(message);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────────

    protected T Map<T>(object source) => Mapper.Map<T>(source);

    protected T Map<T>(object source, T destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        object? mapped = Mapper.Map(source, (object)destination);
        return mapped is null ? throw new InvalidOperationException("Mapping resulted in null.") : (T)mapped;
    }
}
