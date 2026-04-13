using Microsoft.EntityFrameworkCore.Diagnostics;
using Muonroi.Governance.Abstractions.License;
using NSubstitute;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class LicenseSaveChangesInterceptorTests
{
    private sealed class InterceptorTestDbContext(DbContextOptions<InterceptorTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public async Task SaveChangesAsync_With_Interceptor_Enforces_License()
    {
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        LicenseConfigs configs = new() { EnforceOnDatabase = true };

        DbContextOptions<InterceptorTestDbContext> options = new DbContextOptionsBuilder<InterceptorTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .AddInterceptors(new LicenseSaveChangesInterceptor(guard, configs))
            .Options;

        using InterceptorTestDbContext ctx = new(options);
        ctx.Users.Add(new MUser
        {
            UserName = "interceptor-test",
            EmailAddress = "test@test.com",
            Name = "Test",
            Surname = "User",
            Password = "p@ss"
        });
        await ctx.SaveChangesAsync();

        guard.Received().EnsureValid("db.savechanges", Arg.Any<string>());
        guard.Received().RecordAction(Arg.Any<LicenseActionContext>());
    }

    [Fact]
    public async Task SaveChangesAsync_Without_Enforcement_Does_Not_Call_Guard()
    {
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        LicenseConfigs configs = new() { EnforceOnDatabase = false };

        DbContextOptions<InterceptorTestDbContext> options = new DbContextOptionsBuilder<InterceptorTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .AddInterceptors(new LicenseSaveChangesInterceptor(guard, configs))
            .Options;

        using InterceptorTestDbContext ctx = new(options);
        ctx.Users.Add(new MUser
        {
            UserName = "no-enforce-test",
            EmailAddress = "test@test.com",
            Name = "Test",
            Surname = "User",
            Password = "p@ss"
        });
        await ctx.SaveChangesAsync();

        guard.DidNotReceive().EnsureValid("db.savechanges", Arg.Any<string>());
    }

    [Fact]
    public void SaveChanges_Sync_With_Enforcement_Calls_Guard()
    {
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();
        LicenseConfigs configs = new() { EnforceOnDatabase = true };

        DbContextOptions<InterceptorTestDbContext> options = new DbContextOptionsBuilder<InterceptorTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .AddInterceptors(new LicenseSaveChangesInterceptor(guard, configs))
            .Options;

        using InterceptorTestDbContext ctx = new(options);
        ctx.Users.Add(new MUser
        {
            UserName = "sync-test",
            EmailAddress = "test@test.com",
            Name = "Test",
            Surname = "User",
            Password = "p@ss"
        });
        ctx.SaveChanges();

        guard.Received().EnsureValid("db.savechanges", Arg.Any<string>());
    }
}
