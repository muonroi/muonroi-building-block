using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Muonroi.Logging.Abstractions;
using Muonroi.AspNetCore.Services;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Mediator.Mediator.Interfaces;

// MBB001-exempt: test helper — cannot inject IMDateTimeService in test fixtures

namespace Muonroi.AspNetCore.Tests.Helpers;

public enum TestPerm
{
    One = 1,
    Read = 2,
    Write = 3,
    Delete = 4,
    Admin = 5
}

public class FakeMediator : IMediator
{
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : Muonroi.Mediator.Mediator.Interfaces.INotification => Task.CompletedTask;
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => Task.FromResult(default(TResponse)!);
    public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
}

public class TestDbContext : MDbContext
{
    public TestDbContext(DbContextOptions options) : base(options, new FakeMediator())
    {
    }

    public TestDbContext(DbContextOptions options, IMediator mediator, ILicenseGuard? licenseGuard = null,
        IMLog<MDbContext>? logger = null)
        : base(options, mediator, licenseGuard, logger)
    {
    }
}

public class FaultyDbContext(DbContextOptions<FaultyDbContext> options) : TestDbContext(options)
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new Exception("Database failure");
    }

    public override int SaveChanges()
    {
        throw new Exception("Database failure");
    }
}

// MBB002-exempt: test helper — cannot inject IMJsonSerializeService in test fixtures
public class FakeJsonSerializeService : IMJsonSerializeService
{
    public string Serialize<T>(T obj) => System.Text.Json.JsonSerializer.Serialize(obj);
    public T? Deserialize<T>(string text) => System.Text.Json.JsonSerializer.Deserialize<T>(text);
}

public class FakeDateTimeService : IMDateTimeService
{
    public DateTime Now() => DateTime.Now; // MBB001-exempt: test helper
    public DateTime UtcNow() => DateTime.UtcNow; // MBB001-exempt: test helper
    public DateTime Today() => DateTime.Today; // MBB001-exempt: test helper
    public DateTime UtcToday() => DateTime.UtcNow.Date; // MBB001-exempt: test helper
    public double NowTs() => DateTimeOffset.Now.ToUnixTimeSeconds();
    public double UtcNowTs() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public class TestLicenseGuard : ILicenseGuard
{
    private static readonly LicenseState State = LicenseState.CreateFree();
    public LicenseState Current => State;
    public LicenseTier Tier => State.Tier;
    public bool IsFreeMode => true;

    public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
        string? correlationId = null)
    {
    }

    public bool HasFeature(string featureName) => true;
    public void EnsureFeature(string featureName) { }
    public void RecordAction(LicenseActionContext context) { }
    public string GetChainToken() => "TEST_CHAIN";

    public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
    {
        return decryptor("test-key", encryptedData);
    }
}
