using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Web.Repositories;

/// <summary>
/// Site-aware repository base class that extends <see cref="MRepository{T}"/> with per-site
/// DbContext resolution via <see cref="ISiteProfileResolver"/>.
///
/// <para>
/// Consumers derive from this class and get automatic per-site DbContext binding:
/// <code>
/// public class MyEntityRepository : MSiteRepository&lt;MySiteContext, MyEntity&gt;
/// {
///     public MyEntityRepository(
///         MySiteContext siteContext,
///         ISiteProfileResolver siteResolver,
///         IAuthenticateInfoContext authContext,
///         ILicenseGuard licenseGuard,
///         IMDateTimeService dateTimeService)
///         : base(siteContext, siteResolver, authContext, licenseGuard, dateTimeService) { }
///
///     public async Task&lt;MyEntity?&gt; FindByCodeAsync(string code) =>
///         await SiteContext.MyEntities.FirstOrDefaultAsync(e => e.Code == code);
/// }
/// </code>
/// </para>
/// </summary>
/// <typeparam name="TContext">
/// The per-site DbContext type (e.g., <c>MySiteContext</c>).
/// Must inherit from <see cref="MDbContext"/> so it can be passed to <see cref="MRepository{T}"/>'s constructor.
/// The <typeparamref name="TContext"/> instance is resolved from DI — registered via
/// <see cref="SiteProfileDbContextExtensions.AddSiteDbContext{TContext}"/>.
/// </typeparam>
/// <typeparam name="T">
/// The entity type managed by this repository. Must inherit from <see cref="MEntity"/>.
/// </typeparam>
public class MSiteRepository<TContext, T> : MRepository<T>, IMSiteRepository<TContext, T>
    where TContext : MDbContext
    where T : MEntity
{
    /// <summary>
    /// Gets the site-specific DbContext for the current request.
    /// Provides type-safe access to site-scoped tables and queries without manual casting.
    /// </summary>
    protected TContext SiteContext { get; }

    /// <summary>
    /// Gets the site profile resolver for the current request.
    /// Use <see cref="ISiteProfileResolver.Current"/> to access site metadata (e.g., site ID, configuration).
    /// </summary>
    protected ISiteProfileResolver SiteResolver { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MSiteRepository{TContext, T}"/>.
    /// </summary>
    /// <param name="siteContext">
    /// The per-site DbContext resolved from DI (registered via AddSiteDbContext&lt;TContext&gt;).
    /// Passed to <see cref="MRepository{T}"/> as the base DbContext.
    /// </param>
    /// <param name="siteResolver">
    /// Resolves the current site profile. Scoped — one instance per HTTP request.
    /// </param>
    /// <param name="authContext">Authentication context for current user resolution.</param>
    /// <param name="licenseGuard">License guard for feature enforcement.</param>
    /// <param name="dateTimeService">Date-time service for consistent UTC timestamps.</param>
    /// <param name="logger">The logger for the repository.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="siteContext"/> or <paramref name="siteResolver"/> is null.
    /// </exception>
    public MSiteRepository(
        TContext siteContext,
        ISiteProfileResolver siteResolver,
        IAuthenticateInfoContext authContext,
        ILicenseGuard licenseGuard,
        IMDateTimeService dateTimeService,
        IMLog<MSiteRepository<TContext, T>>? logger = null)
        : base(siteContext, authContext, licenseGuard, dateTimeService, logger)
    {
        SiteContext = MGuard.NotNull(siteContext);
        SiteResolver = MGuard.NotNull(siteResolver);
    }
}
