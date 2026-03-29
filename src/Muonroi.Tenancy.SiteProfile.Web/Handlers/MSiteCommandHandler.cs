using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Web.Configuration;

namespace Muonroi.Tenancy.SiteProfile.Web.Handlers;

/// <summary>
/// Lightweight value type that carries the result of <see cref="MSiteCommandHandler{TRequest,TResponse}.ValidateAsync"/>.
/// </summary>
/// <param name="IsValid">Whether validation passed.</param>
/// <param name="Error">The error message when <paramref name="IsValid"/> is <c>false</c>; otherwise <c>null</c>.</param>
public sealed record ValidationResult(bool IsValid, string? Error = null)
{
    /// <summary>A successful validation result (no error).</summary>
    public static readonly ValidationResult Success = new(true);

    /// <summary>Creates a failed validation result with the specified <paramref name="error"/> message.</summary>
    /// <param name="error">Human-readable reason for the validation failure.</param>
    public static ValidationResult Failure(string error) => new(false, error);
}

/// <summary>
/// Abstract base class for per-site MediatR command handlers in the Muonroi ecosystem.
///
/// Mirrors the <c>MSiteService&lt;TContext, TEntity&gt;</c> pattern (Phase 51): protected properties
/// expose site infrastructure, and a template method pattern separates validation from execution
/// so each derived handler focuses only on its site-specific concerns.
///
/// <para><b>Design goals:</b></para>
/// <list type="bullet">
///   <item>Eliminate SiteCode if/switch branching in aggregate handlers — dispatch is enforced at
///   compile-time via physical class inheritance and keyed DI.</item>
///   <item>Expose <see cref="SiteResolver"/> and <see cref="SiteConfig"/> as protected properties —
///   derived classes access both without casting.</item>
///   <item><see cref="Handle"/> is <c>sealed</c> — it always calls <see cref="ValidateAsync"/> first,
///   throwing on failure, then delegates to <see cref="ExecuteAsync"/>.</item>
/// </list>
///
/// <para><b>Usage example:</b></para>
/// <example>
/// <code>
/// // Per-site handler — MySite
/// public class MySiteHandler : MSiteCommandHandler&lt;MyCommand, MyResponse&gt;
/// {
///     public MySiteHandler(ISiteProfileResolver siteResolver, ISiteConfiguration siteConfig)
///         : base(siteResolver, siteConfig) { }
///
///     protected override async Task&lt;ValidationResult&gt; ValidateAsync(
///         MyCommand request, CancellationToken cancellationToken)
///     {
///         // Site-specific: validate date window
///         if (request.TargetDate &lt; DateTime.UtcNow)
///             return ValidationResult.Failure("TargetDate must be in the future.");
///         return ValidationResult.Success;
///     }
///
///     protected override async Task&lt;MyResponse&gt; ExecuteAsync(
///         MyCommand request, CancellationToken cancellationToken)
///     {
///         var siteCode = SiteResolver.Current.SiteCode;
///         var maxSlots = SiteConfig.GetValue&lt;int&gt;("MaxSlots", defaultValue: 10);
///         // ... site-specific business logic using siteCode and maxSlots
///         return new MyResponse { ... };
///     }
/// }
///
/// // Registration (MySiteSiteProfile.RegisterServices):
/// services.AddKeyedScoped&lt;IRequestHandler&lt;MyCommand, MyResponse&gt;,
///     MySiteHandler&gt;("MySite");
///
/// // Program.cs:
/// services.AddSiteCommandHandler&lt;MyCommand, MyResponse&gt;();
/// </code>
/// </example>
/// </summary>
/// <typeparam name="TRequest">The command/request type implementing <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public abstract class MSiteCommandHandler<TRequest, TResponse>
    : IMSiteCommandHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Resolves the current site profile (<c>SiteCode</c>, <c>SiteId</c>, etc.) for the incoming request.
    /// </summary>
    protected ISiteProfileResolver SiteResolver { get; }

    /// <summary>
    /// Provides per-site configuration values read from the <c>Sites:{SiteId}:*</c> section
    /// of <c>appsettings.json</c>. Values reflect the latest appsettings state on every call
    /// (transparent hot-reload — no <c>IOptionsMonitor</c> overhead).
    /// </summary>
    protected ISiteConfiguration SiteConfig { get; }

    /// <summary>
    /// Initialises the handler with the required site infrastructure.
    /// </summary>
    /// <param name="siteResolver">Resolves the current <see cref="ISiteProfile"/>. Must not be null.</param>
    /// <param name="siteConfig">Per-site configuration overlay. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    protected MSiteCommandHandler(ISiteProfileResolver siteResolver, ISiteConfiguration siteConfig)
    {
        ArgumentNullException.ThrowIfNull(siteResolver);
        ArgumentNullException.ThrowIfNull(siteConfig);
        SiteResolver = siteResolver;
        SiteConfig = siteConfig;
    }

    /// <summary>
    /// Optional site-specific validation step. The default implementation returns
    /// <see cref="ValidationResult.Success"/> — simple handlers need not override this.
    ///
    /// Override to add precondition checks (e.g., date window validation, business rule constraints)
    /// without mixing them into <see cref="ExecuteAsync"/>.
    /// </summary>
    /// <param name="request">The incoming command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="ValidationResult.Success"/> if the request is valid;
    /// otherwise <see cref="ValidationResult.Failure(string)"/> with an error description.
    /// </returns>
    protected virtual Task<ValidationResult> ValidateAsync(
        TRequest request, CancellationToken cancellationToken)
        => Task.FromResult(ValidationResult.Success);

    /// <summary>
    /// Site-specific business logic. Must be overridden by each per-site derived class.
    ///
    /// Only called when <see cref="ValidateAsync"/> returns a successful result.
    /// </summary>
    /// <param name="request">The incoming command (already validated).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The handler response.</returns>
    protected abstract Task<TResponse> ExecuteAsync(
        TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Pipeline entry-point called by the MediatR/Mediator dispatcher.
    ///
    /// Execution order:
    /// <list type="number">
    ///   <item>Call <see cref="ValidateAsync"/> — if it fails, throw <see cref="InvalidOperationException"/>.</item>
    ///   <item>Delegate to <see cref="ExecuteAsync"/> and return its result.</item>
    /// </list>
    ///
    /// This method is not virtual — the orchestration sequence is fixed and should not be overridden
    /// by derived classes (override <see cref="ValidateAsync"/> or <see cref="ExecuteAsync"/> instead).
    /// </summary>
    /// <param name="request">The incoming command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The handler response produced by <see cref="ExecuteAsync"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="ValidateAsync"/> returns a failure result.
    /// </exception>
    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        ValidationResult result = await ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
            throw new InvalidOperationException($"Validation failed: {result.Error}");
        return await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
