using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Enforces license checks when DbContext saves changes.
/// </summary>
/// <param name="guard">The license guard.</param>
/// <param name="configs">License configuration.</param>
public sealed class LicenseSaveChangesInterceptor(
    ILicenseGuard guard,
    LicenseConfigs configs)
    : SaveChangesInterceptor
{
    private const string ActionType = "db.savechanges";

    /// <summary>
    /// Performs license validation before changes are saved.
    /// </summary>
    /// <param name="eventData">The event data.</param>
    /// <param name="result">The current interception result.</param>
    /// <returns>The interception result.</returns>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (LicenseExecutionContext.IsInLicenseCheck)
        {
            return base.SavingChanges(eventData, result);
        }

        if (ShouldEnforceOnDatabase())
        {
            using (LicenseExecutionContext.BeginScope())
            {
                guard.EnsureValid(ActionType, eventData.Context?.GetType().Name);
                LicenseActionContext context = new()
                {
                    ActionType = ActionType,
                    ActionName = eventData.Context?.GetType().Name,
                    PayloadHash = ComputeChangeHash(eventData.Context),
                    TenantId = TenantContext.CurrentTenantId
                };
                guard.RecordAction(context);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Performs license validation before changes are saved asynchronously.
    /// </summary>
    /// <param name="eventData">The event data.</param>
    /// <param name="result">The current interception result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The interception result.</returns>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (LicenseExecutionContext.IsInLicenseCheck)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        if (ShouldEnforceOnDatabase())
        {
            using (LicenseExecutionContext.BeginScope())
            {
                guard.EnsureValid(ActionType, eventData.Context?.GetType().Name);
                LicenseActionContext context = new()
                {
                    ActionType = ActionType,
                    ActionName = eventData.Context?.GetType().Name,
                    PayloadHash = ComputeChangeHash(eventData.Context),
                    TenantId = TenantContext.CurrentTenantId
                };
                guard.RecordAction(context);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private bool ShouldEnforceOnDatabase()
    {
        return configs.EnforceOnDatabase;
    }

    private static string ComputeChangeHash(DbContext? context)
    {
        if (context is null)
        {
            return string.Empty;
        }

        IOrderedEnumerable<string> entries = context.ChangeTracker.Entries()
            .Select(e => $"{e.Entity.GetType().Name}:{e.State}")
            .OrderBy(x => x);
        string payload = string.Join("|", entries);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
