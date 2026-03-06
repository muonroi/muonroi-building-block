namespace Muonroi.Data.Abstractions.UnitOfWork;

public interface IMUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<Guid> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
