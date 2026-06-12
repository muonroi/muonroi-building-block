namespace Muonroi.BuildingBlock.Test;

public class ConfigurationDependencyInjectionTests
{
    private class DummyRepo : IMRepository<MUser>
    {
        public IMUnitOfWork UnitOfWork => throw new NotImplementedException();

        public MUser Add(MUser newEntity) => newEntity;
        public Task<int> UpdateAsync(MUser updateEntity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> DeleteAsync(MUser deleteEntity, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> AddBatchAsync(IEnumerable<MUser> newEntities, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> AddOrUpdateBatchAsync(IEnumerable<MUser> newEntities, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> UpdateBatchAsync(Expression<Func<MUser, bool>> predicate, Expression<Func<MUser, MUser>> updateExpression, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteBatchAsync(Expression<Func<MUser, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteBatchAsync(IEnumerable<MUser> deleteEntities, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> SoftRestoreAsync(MUser entity, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task BulkInsertAsync(IEnumerable<MUser> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> ExecuteStoredProcedureAsync(string storedProcedureName, CancellationToken cancellationToken = default, params object[] parameters) => Task.FromResult(0);
        public Task<TResult> ExecuteStoredProcedureScalarAsync<TResult>(string storedProcedureName, CancellationToken cancellationToken = default, params object[] parameters) => Task.FromResult(default(TResult)!);
    }

    [Fact]
    public void AddScopeServices_Registers_Service()
    {
        ServiceCollection services = [];
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("dep"));
        services.AddScoped<MDbContext, TestDbContext>();
        services.AddScoped(_ => new MAuthenticateInfoContext(false));
        services.AddScopeServices(typeof(DummyRepo).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        IMRepository<MUser>? repo = provider.GetService<IMRepository<MUser>>();
        Assert.NotNull(repo);
    }

    [Fact]
    public void AddScopeServices_NullServices_Throws()
    {
        IServiceCollection? services = null;
        Assert.ThrowsAny<Exception>(() =>
            services!.AddScopeServices(typeof(DummyRepo).Assembly));
    }

    [Fact]
    public void AddScopeServices_Duplicate_Adds_Twice()
    {
        ServiceCollection services = [];
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("dep"));
        services.AddScoped<MDbContext, TestDbContext>();
        services.AddScoped(_ => new MAuthenticateInfoContext(false));
        services.AddScopeServices(typeof(DummyRepo).Assembly);
        services.AddScopeServices(typeof(DummyRepo).Assembly);
        int count = services.Count(d =>
            d.ServiceType == typeof(IMRepository<MUser>) && d.ImplementationType == typeof(DummyRepo));   
        Assert.Equal(2, count);
    }
}
