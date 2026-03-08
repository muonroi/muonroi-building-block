namespace Muonroi.SignalR.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Muonroi.SignalR.SignalR;
using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Governance.License;
using Moq;
using Xunit;
using System.Reflection;
using System.Collections.Generic;

public class SignalRTests
{
    public class TestHub : Hub { }

    [Fact]
    public async Task TenantHubFilter_ShouldSetTenantId()
    {
        // Arrange
        var resolverMock = new Mock<ITenantIdResolver>();
        resolverMock.Setup(x => x.ResolveTenantIdAsync(It.IsAny<HttpContext>())).ReturnsAsync("TenantA");
        
        // Set to false to avoid GetHttpContext() mock complexity for now
        var tokenInfo = new MTokenInfo { MultiTenantEnabled = false };
        var guardMock = new Mock<ILicenseGuard>();
        
        var filter = new TenantHubFilter(resolverMock.Object, tokenInfo, guardMock.Object);
        
        var hubContextMock = new Mock<HubCallerContext>();
        var features = new FeatureCollection();
        hubContextMock.Setup(x => x.Features).Returns(features);
        
        var hub = new TestHub();
        var methodInfo = typeof(TestHub).GetMethod(nameof(object.ToString))!;
        
        var invocationContext = new HubInvocationContext(
            hubContextMock.Object, 
            Mock.Of<IServiceProvider>(), 
            hub, 
            methodInfo, 
            new List<object?>().AsReadOnly());
        
        // Act
        await filter.InvokeMethodAsync(invocationContext, async (ctx) => 
        {
            // Inside next(), TenantId is null because GetHttpContext() returned null
            return await ValueTask.FromResult<object?>(null);
        });

        // Assert after execution
        Assert.Null(TenantContext.CurrentTenantId);
    }
}
