namespace Muonroi.Data.Abstractions.UnitOfWork;

public interface IMDataContext : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
