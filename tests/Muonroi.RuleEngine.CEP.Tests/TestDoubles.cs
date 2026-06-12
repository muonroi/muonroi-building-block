using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.RuleEngine.CEP.Tests;

internal sealed class StubDateTimeService(DateTime utcNow) : IMDateTimeService
{
    private readonly DateTime _utcNow = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();

    public DateTime Now()
    {
        return _utcNow.ToLocalTime();
    }

    public DateTime UtcNow()
    {
        return _utcNow;
    }

    public DateTime Today()
    {
        return _utcNow.ToLocalTime().Date;
    }

    public DateTime UtcToday()
    {
        return _utcNow.Date;
    }

    public double NowTs()
    {
        return new DateTimeOffset(Now()).ToUnixTimeSeconds();
    }

    public double UtcNowTs()
    {
        return new DateTimeOffset(_utcNow).ToUnixTimeSeconds();
    }
}

internal sealed class StubMediator : IMediator
{
    public Task<MResponse> Send<MResponse>(IRequest<MResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        return Task.CompletedTask;
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<object?>(null);
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        return Task.CompletedTask;
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<MResponse> CreateStream<MResponse>(
        IStreamRequest<MResponse> request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<object?> CreateStream(
        object request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}
