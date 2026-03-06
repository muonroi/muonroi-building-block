namespace Muonroi.BuildingBlock.Test;

public class DependencyInternalInjectionTests
{
    private class DummyRepo : IMRepository<MUser>
    {
        public IMUnitOfWork UnitOfWork => throw new NotImplementedException();

        public MUser Add(MUser newEntity)
        {
            return newEntity;
        }

        public Task<int> UpdateAsync(MUser updateEntity)
        {
            return Task.FromResult(0);
        }

        public Task<bool> DeleteAsync(MUser deleteEntity)
        {
            return Task.FromResult(true);
        }

        public Task ExecuteTransactionAsync(Func<Task<MVoidMethodResult>> action)
        {
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync()
        {
            return Task.CompletedTask;
        }

        public Task<int> AddBatchAsync(IEnumerable<MUser> newEntities)
        {
            return Task.FromResult(0);
        }

        public Task<int> AddOrUpdateBatchAsync(IEnumerable<MUser> newEntities)
        {
            return Task.FromResult(0);
        }

        public Task<int> UpdateBatchAsync(Expression<Func<MUser, bool>> predicate, Action<MUser> updateAction)
        {
            return Task.FromResult(0);
        }

        public Task<int> DeleteBatchAsync(Expression<Func<MUser, bool>> predicate)
        {
            return Task.FromResult(0);
        }

        public Task<int> DeleteBatchAsync(IEnumerable<MUser> deleteEntities)
        {
            return Task.FromResult(0);
        }

        public Task<bool> SoftRestoreAsync(MUser entity)
        {
            return Task.FromResult(true);
        }

        public Task BulkInsertAsync(IEnumerable<MUser> entities)
        {
            return Task.CompletedTask;
        }

        public Task<int> ExecuteStoredProcedureAsync(string storedProcedureName, params object[] parameters)
        {
            return Task.FromResult(0);
        }

        public Task<TResult> ExecuteStoredProcedureScalarAsync<TResult>(string storedProcedureName,
            params object[] parameters)
        {
            return Task.FromResult(default(TResult)!);
        }
    }

    [Fact]
    public void ResolveDependencyScope_Registers_Generic_Service()
    {
        ServiceCollection services = [];
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("dep"));
        services.AddScoped<MDbContext, TestDbContext>();
        services.AddScoped(_ => new MAuthenticateInfoContext(false));
        services.ResolveDependencyScope(typeof(DummyRepo).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        IMRepository<MUser>? repo = provider.GetService<IMRepository<MUser>>();
        Assert.NotNull(repo);
    }

    [Fact]
    public void ResolveDependencyScope_No_Services_Returns_Null_On_Resolve()
    {
        ServiceCollection services = [];
        services.ResolveDependencyScope(typeof(string).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        IMRepository<MUser>? repo = provider.GetService<IMRepository<MUser>>();
        Assert.Null(repo);
    }

    [Fact]
    public void ResolveDependencyScope_Resolve_Missing_Service_Throws()
    {
        ServiceCollection services = [];
        services.ResolveDependencyScope(typeof(string).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IMRepository<MUser>>());
    }

    [Fact]
    public void ResolveDependencyScope_Null_Assembly_No_Error()
    {
        ServiceCollection services = [];
        Exception ex = Record.Exception(() => services.ResolveDependencyScope(null!));
        Assert.Null(ex);
        ServiceProvider provider = services.BuildServiceProvider();
        IMRepository<MUser>? repo = provider.GetService<IMRepository<MUser>>();
        Assert.Null(repo);
    }
}
