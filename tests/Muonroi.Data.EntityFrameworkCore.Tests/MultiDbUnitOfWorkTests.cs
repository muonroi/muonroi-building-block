namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MultiDbUnitOfWorkTests
{
    private sealed class TrackingDataContext(int saveChangesResult) : IMDataContext
    {
        public int SaveChangesResult { get; } = saveChangesResult;
        public int SaveChangesCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public bool Disposed { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(SaveChangesResult);
        }

        public void Dispose()
        {
            DisposeCalls++;
            Disposed = true;
        }
    }

    [Fact]
    public async Task SaveChangesAcrossContexts()
    {
        TrackingDataContext first = new(1);
        TrackingDataContext second = new(2);

        using MultiDbUnitOfWork unitOfWork = new(first, second);
        int result = await unitOfWork.SaveChangesAsync();

        Assert.Equal(3, result);
        Assert.Equal(1, first.SaveChangesCalls);
        Assert.Equal(1, second.SaveChangesCalls);
    }

    [Fact]
    public void Dispose_Handles_Multiple_Calls()
    {
        TrackingDataContext first = new(0);
        TrackingDataContext second = new(0);

        MultiDbUnitOfWork unitOfWork = new(first, second);
        unitOfWork.Dispose();
        unitOfWork.Dispose();

        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
        Assert.Equal(2, first.DisposeCalls);
        Assert.Equal(2, second.DisposeCalls);
    }
}
