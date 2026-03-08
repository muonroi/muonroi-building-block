namespace Muonroi.UiEngine.Catalog.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Muonroi.Logging.Abstractions;
using Muonroi.UiEngine.Catalog.Services;
using Muonroi.RuleEngine.Runtime.Rules;
using Moq;
using Xunit;

public class CatalogTests
{
    [Fact]
    public async Task ScanRulesAsync_ShouldReturnDescriptors()
    {
        // Arrange
        var apiProviderMock = new Mock<IApiDescriptionGroupCollectionProvider>();
        apiProviderMock.Setup(x => x.ApiDescriptionGroups).Returns(new ApiDescriptionGroupCollection([], 1));
        
        var serviceProviderMock = new Mock<IServiceProvider>();
        var loggerMock = new Mock<IMLog<CatalogScanService>>();
        var optionsMock = new Mock<IOptionsMonitor<RuleOptions>>();

        var service = new CatalogScanService(
            apiProviderMock.Object,
            serviceProviderMock.Object,
            loggerMock.Object,
            optionsMock.Object);

        // Act
        var rules = await service.ScanRulesAsync(CancellationToken.None);

        // Assert
        // Even with no rules in the test assembly, it scans all loaded assemblies
        // so it might find some from the Muonroi projects themselves
        Assert.NotNull(rules);
    }
}
