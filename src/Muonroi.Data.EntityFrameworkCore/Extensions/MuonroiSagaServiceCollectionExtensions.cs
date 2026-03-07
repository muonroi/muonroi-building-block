using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Data.EntityFrameworkCore.Saga;

namespace Muonroi.Data.EntityFrameworkCore.Extensions;

public static class MuonroiSagaServiceCollectionExtensions
{
    public static IServiceCollection AddMuonroiSagaDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
        where TContext : MSagaDbContext
    {
        services.AddDbContext<TContext>(optionsAction);
        return services;
    }
}
