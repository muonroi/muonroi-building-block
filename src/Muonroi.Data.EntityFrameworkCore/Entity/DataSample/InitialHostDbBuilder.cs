using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

public class InitialHostDbBuilder<TContext>(TContext context, IMDateTimeService dateTimeService)
    where TContext : MDbContext
{
    public void Create()
    {
        new DefaultLanguagesCreator<TContext>(context, dateTimeService).Create();
        new HostRoleAndUserCreator<TContext>(context, dateTimeService).Create();

        context.SaveChanges();
    }
}
