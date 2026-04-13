
namespace Muonroi.BuildingBlock.Test.MultiTenant;

using Microsoft.Extensions.Logging;
using Muonroi.Governance.License;

public class FakeMediator : IMediator
{
    public IAsyncEnumerable<MResponse> CreateStream<MResponse>(IStreamRequest<MResponse> request, CancellationToken cancellationToken = default) => AsyncEnumerable.Empty<MResponse>();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => AsyncEnumerable.Empty<object?>();
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    public Task<MResponse> Send<MResponse>(IRequest<MResponse> request, CancellationToken cancellationToken = default) => Task.FromResult(default(MResponse)!);
    public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.FromResult<object?>(null);

}

public class TestDbContext : MDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options, new FakeMediator())
    {
    }

    public TestDbContext(DbContextOptions options, IMediator mediator, ILicenseGuard? licenseGuard = null,
        ILogger<TestDbContext>? logger = null)
        : base(options, mediator, licenseGuard, logger)
    {
    }
}
