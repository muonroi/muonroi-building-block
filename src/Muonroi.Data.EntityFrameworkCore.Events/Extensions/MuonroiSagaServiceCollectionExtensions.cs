using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Data.EntityFrameworkCore.Saga;

namespace Muonroi.Data.EntityFrameworkCore.Extensions;

/// <summary>
/// Service collection extensions for saga persistence.
/// </summary>
public static class MuonroiSagaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a saga <see cref="DbContext"/> with the provided options.
    /// </summary>
    /// <typeparam name="TContext">The saga context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsAction">The options configuration action.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddMuonroiSagaDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> optionsAction)
        where TContext : MSagaDbContext
    {
        services.AddDbContext<TContext>(optionsAction);
        return services;
    }
}
