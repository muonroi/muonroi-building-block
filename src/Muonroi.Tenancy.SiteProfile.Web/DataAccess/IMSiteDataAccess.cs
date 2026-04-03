using Muonroi.Tenancy.SiteProfile.Web.Dapper;

namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Unified data access facade for a site-scoped DbContext.
///
/// Combines EF Core write operations, Dapper read operations, and escape hatches into a single
/// per-context abstraction. Consumers inject <c>IMSiteDataAccess&lt;TContext&gt;</c> instead of
/// separate EF/Dapper dependencies, removing boilerplate while keeping full flexibility.
///
/// <para><b>Domain event dispatch</b> (D-05): Calling <see cref="SaveChangesAsync"/> triggers
/// automatic domain event dispatch via <c>MDbContext.SaveChangesAsync()</c>, which publishes
/// all pending domain events through IMediator — no manual event wiring required.</para>
///
/// <para><b>Column resolution</b>: All <see cref="QueryAsync{TResult}"/>,
/// <see cref="QueryFirstOrDefaultAsync{TResult}"/>, and <see cref="ExecuteAsync"/> methods
/// accept SQL with <c>[[PropertyName]]</c> markers that are auto-resolved to site-specific
/// column names via <see cref="SiteSqlBuilder"/> before Dapper execution.</para>
///
/// Registration example:
/// <code>
/// services.AddSiteDataAccess&lt;MySiteContext&gt;();
/// </code>
/// </summary>
/// <typeparam name="TContext">
/// The per-site DbContext type (e.g., <c>TciOrderContext</c>). Must inherit from <see cref="MDbContext"/>.
/// </typeparam>
public interface IMSiteDataAccess<TContext> where TContext : MDbContext
{
    // -------------------------------------------------------------------------
    // Write surface (D-02) — delegates to EF Core DbSet operations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a new entity to the EF change tracker (pending insert on <see cref="SaveChangesAsync"/>).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity instance to add. Must not be null.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteAsync<T>(T entity, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Adds a collection of entities to the EF change tracker (pending bulk insert on <see cref="SaveChangesAsync"/>).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to add. Must not be null.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteManyAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Marks an existing entity as modified in the EF change tracker (pending update on <see cref="SaveChangesAsync"/>).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity instance to update. Must not be null.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Marks an entity for removal from the EF change tracker (pending delete on <see cref="SaveChangesAsync"/>).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity instance to remove. Must not be null.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync<T>(T entity, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Persists all pending EF changes to the database and dispatches domain events (D-05).
    /// Domain event dispatch is automatic via <c>MDbContext.SaveChangesAsync()</c> — no manual
    /// event publishing required.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // Read surface (D-03) — delegates to ISiteQueryExecutor (Dapper + auto-column resolution)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Executes a SQL query and returns a collection of results.
    /// <para>
    /// Accepts SQL with <c>[[PropertyName]]</c> markers that are auto-interpolated to site-specific
    /// column names via <see cref="SiteSqlBuilder.InterpolateMarkers"/> before Dapper execution.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">The result DTO type. Dapper maps column aliases to properties by name.</typeparam>
    /// <param name="markerSql">SQL with optional <c>[[PropertyName]]</c> markers.</param>
    /// <param name="param">Optional Dapper parameters (anonymous object or DynamicParameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An enumerable of <typeparamref name="TResult"/> instances.</returns>
    Task<IEnumerable<TResult>> QueryAsync<TResult>(string markerSql, object? param = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a SQL query and returns the first result, or <c>null</c> if no rows are found.
    /// <para>
    /// Accepts SQL with <c>[[PropertyName]]</c> markers that are auto-interpolated to site-specific
    /// column names via <see cref="SiteSqlBuilder.InterpolateMarkers"/> before Dapper execution.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">The result DTO type.</typeparam>
    /// <param name="markerSql">SQL with optional <c>[[PropertyName]]</c> markers.</param>
    /// <param name="param">Optional Dapper parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first <typeparamref name="TResult"/>, or <c>null</c> if no rows found.</returns>
    Task<TResult?> QueryFirstOrDefaultAsync<TResult>(string markerSql, object? param = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a non-query SQL statement (INSERT, UPDATE, DELETE) and returns the row count.
    /// <para>
    /// Accepts SQL with <c>[[PropertyName]]</c> markers that are auto-interpolated to site-specific
    /// column names via <see cref="SiteSqlBuilder.InterpolateMarkers"/> before Dapper execution.
    /// </para>
    /// </summary>
    /// <param name="markerSql">SQL with optional <c>[[PropertyName]]</c> markers.</param>
    /// <param name="param">Optional Dapper parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    Task<int> ExecuteAsync(string markerSql, object? param = null, CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // Escape hatches (D-04) — direct access for advanced scenarios
    // -------------------------------------------------------------------------

    /// <summary>
    /// Provides direct access to the underlying site DbContext for advanced EF Core scenarios
    /// (raw queries, LINQ, transactions, model metadata access).
    /// </summary>
    TContext Context { get; }

    /// <summary>
    /// Provides direct access to the <see cref="SiteSqlBuilder"/> for manual SQL construction
    /// (column lists, SELECT clauses, WHERE conditions) outside the marker-interpolation flow.
    /// </summary>
    SiteSqlBuilder Sql { get; }
}
