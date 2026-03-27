using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

/// <summary>
/// Builds initial host data for a new database.
/// </summary>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <param name="context">The database context.</param>
/// <param name="dateTimeService">The date/time service.</param>
public class InitialHostDbBuilder<TContext>(TContext context, IMDateTimeService dateTimeService)
    where TContext : MDbContext
{
    /// <summary>
    /// Creates initial host data (languages, roles, users).
    /// </summary>
    public void Create()
    {
        new DefaultLanguagesCreator<TContext>(context, dateTimeService).Create();
        new HostRoleAndUserCreator<TContext>(context, dateTimeService).Create();

        context.SaveChanges();
    }
}
