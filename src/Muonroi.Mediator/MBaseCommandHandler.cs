using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
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
    /// <summary>
    /// Gets the Pagination Config.
    /// </summary>
    protected MPaginationConfig? PaginationConfig => paginationConfig;
    /// <summary>
    /// Gets the Default Page Index.
    /// </summary>
    protected int DefaultPageIndex => paginationConfig?.DefaultPageIndex ?? 0;
    /// <summary>
    /// Gets the Default Page Size.
    /// </summary>
    protected int DefaultPageSize => paginationConfig?.DefaultPageSize ?? 0;
    /// <summary>
    /// Gets the Max Page Size.
    /// </summary>
    protected int MaxPageSize => paginationConfig?.MaxPageSize ?? 0;

    // ── Auth context (legacy, keep for backward compat) ──────────────────────────
    /// <summary>
    /// Gets the Token Info.
    /// </summary>
    protected IAuthenticateInfoContext TokenInfo => tokenInfo;
    /// <summary>
    /// Gets the Current User Id.
    /// </summary>
    protected string CurrentUserId => tokenInfo.CurrentUserGuid;
    /// <summary>
    /// Gets the Current Username.
    /// </summary>
    protected string CurrentUsername => tokenInfo.CurrentUsername;

    // ── Execution context (ecosystem wrapper) ────────────────────────────────────
    /// <summary>
    /// Gets the Context Accessor.
    /// </summary>
    protected ISystemExecutionContextAccessor ContextAccessor => contextAccessor;
    /// <summary>
    /// Executes the Current Tenant Id operation.
    /// </summary>
    protected string? CurrentTenantId => contextAccessor.Get().TenantId;
    /// <summary>
    /// Executes the Correlation Id operation.
    /// </summary>
    protected string? CorrelationId => contextAccessor.Get().CorrelationId;
    /// <summary>
    /// Executes the Current Permissions operation.
    /// </summary>
    protected IReadOnlyList<string> CurrentPermissions => contextAccessor.Get().Permissions;

    // ── Time (ecosystem wrapper) ─────────────────────────────────────────────────
    /// <summary>
    /// Gets the Date Time Service.
    /// </summary>
    protected IMDateTimeService DateTimeService => dateTimeService;

    // ── Logging ──────────────────────────────────────────────────────────────────
    /// <summary>
    /// Gets the Mapper.
    /// </summary>
    protected IMapper Mapper => mapper;
    /// <summary>
    /// Gets the Logger.
    /// </summary>
    protected IMLog<MBaseCommandHandler> Logger => logger;
    /// <summary>
    /// Gets the Mediator.
    /// </summary>
    protected IMediator Mediator => mediator;

    // ── Timestamp helpers (static — MBB001-exempt: static-class boundary) ────────
    /// <summary>
    /// Executes the Now Ts Only Day operation.
    /// </summary>
    protected static double NowTsOnlyDay => DateTime.UtcNow.GetTimeStamp(); // MBB001-exempt: static-class boundary
    /// <summary>
    /// Executes the Now Ts operation.
    /// </summary>
    protected static double NowTs => DateTime.UtcNow.GetTimeStamp(true);    // MBB001-exempt: static-class boundary
    /// <summary>
    /// Gets the Now.
    /// </summary>
    protected static DateTime Now => DateTime.UtcNow;                        // MBB001-exempt: static-class boundary

    // ── Dispatch helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the Send Async{TResponse} operation.
    /// </summary>
    protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    /// <summary>
    /// Executes the Publish Async{TNotification} operation.
    /// </summary>
    protected async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : INotification
    {
        await Mediator.Publish(notification, cancellationToken);
    }

    // ── Logging helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the Log Info operation.
    /// </summary>
    protected void LogInfo(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        logger?.Info(message);
    }

    /// <summary>
    /// Executes the Log Error operation.
    /// </summary>
    protected void LogError(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        logger.LogError(message);
    }

    /// <summary>
    /// Executes the Log Error operation.
    /// </summary>
    protected void LogError(Exception ex) => logger.LogError(ex, ex.Message);

    /// <summary>
    /// Executes the Log Error operation.
    /// </summary>
    protected void LogError(string message, Exception ex) => logger.LogError(ex, message);

    /// <summary>
    /// Executes the Log Warning operation.
    /// </summary>
    protected void LogWarning(string message)
    {
        MGuard.NotNull(message);
        logger.LogWarning(message);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the Map{T} operation.
    /// </summary>
    protected T Map<T>(object source) => Mapper.Map<T>(source);

    /// <summary>
    /// Executes the Map{T} operation.
    /// </summary>
    protected T Map<T>(object source, T destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        object? mapped = Mapper.Map(source, (object)destination);
        return mapped is null ? throw new MInternalException("Mapping resulted in null.") : (T)mapped;
    }
}
