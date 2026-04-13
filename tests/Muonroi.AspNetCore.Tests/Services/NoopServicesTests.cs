using Muonroi.AspNetCore.Services;
using Muonroi.UiEngine.Catalog.Services;
using Muonroi.Core.Abstractions.Models;
using Xunit;
using System.Reflection;

namespace Muonroi.AspNetCore.Tests.Services;

public class NoopServicesTests
{
    [Fact]
    public async Task NoopCatalogScanService_Methods_ReturnEmpty()
    {
        var type = typeof(IAuthService<,>).Assembly.GetType("Muonroi.AspNetCore.Services.NoopCatalogScanService");
        var service = (ICatalogScanService)Activator.CreateInstance(type!)!;

        Assert.Empty(await service.ScanApisAsync());
        Assert.Empty(await service.ScanRulesAsync());
        Assert.Empty(await service.BuildBindingsAsync());
        Assert.NotNull(await service.BuildGraphAsync());
    }

    [Fact]
    public async Task NoopUiEngineSchemaNotifier_Method_DoesNothing()
    {
        var type = typeof(IAuthService<,>).Assembly.GetType("Muonroi.AspNetCore.Services.NoopUiEngineSchemaNotifier");
        var service = (IUiEngineSchemaNotifier)Activator.CreateInstance(type!)!;

        await service.NotifySchemaChangedAsync(new MUiEngineSchemaVersion());
        // Success if no exception
    }
}
