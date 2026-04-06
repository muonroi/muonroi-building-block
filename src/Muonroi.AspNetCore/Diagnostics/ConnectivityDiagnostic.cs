using Microsoft.EntityFrameworkCore;

namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Critical diagnostic that pings the registered database(s) to confirm connectivity at startup.
/// Skips gracefully when no <see cref="DbContext"/> is registered.
/// </summary>
public sealed class ConnectivityDiagnostic : IMEcosystemDiagnostic
{
    /// <inheritdoc />
    public string Name => "ConnectivityDiagnostic";

    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Critical;

    /// <inheritdoc />
    public async Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        // Try to resolve a DbContext — the abstract DbContext base type covers all EF contexts
        DbContext? dbContext = sp.GetService<DbContext>();
        if (dbContext is null)
        {
            // Try by scanning for known Muonroi base context type
            dbContext = TryResolveByTypeName(sp, "Muonroi.Data.EntityFrameworkCore.Entity.MDbContext");
        }

        if (dbContext is null)
        {
            return DiagnosticResult.Pass("No DbContext registered — skipping database connectivity check.");
        }

        try
        {
            // DatabaseFacade.CanConnectAsync() is available on DbContext.Database directly
            bool canConnect = await dbContext.Database.CanConnectAsync(ct).ConfigureAwait(false);

            return canConnect
                ? DiagnosticResult.Pass($"Database connectivity confirmed for '{dbContext.GetType().Name}'.")
                : DiagnosticResult.Fail(
                    $"Cannot connect to database for '{dbContext.GetType().Name}'.",
                    fixSuggestion: "Check your connection string in appsettings.json and ensure the database server is running.",
                    details: new Dictionary<string, object>
                    {
                        ["contextType"] = dbContext.GetType().FullName ?? dbContext.GetType().Name
                    });
        }
        catch (Exception ex)
        {
            return DiagnosticResult.Fail(
                $"Database connectivity check threw an exception for '{dbContext.GetType().Name}': {ex.Message}",
                fixSuggestion: "Verify connection string, network access, and that the database server is reachable.",
                details: new Dictionary<string, object>
                {
                    ["exceptionType"] = ex.GetType().Name,
                    ["exceptionMessage"] = ex.Message,
                    ["contextType"] = dbContext.GetType().FullName ?? dbContext.GetType().Name
                });
        }
    }

    private static DbContext? TryResolveByTypeName(IServiceProvider sp, string fullyQualifiedName)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? found = asm.GetType(fullyQualifiedName);
            if (found is not null)
            {
                object? svc = sp.GetService(found);
                if (svc is DbContext ctx)
                    return ctx;
            }
        }
        return null;
    }
}
