using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Rules;
using Muonroi.Rules.Controllers;
using Muonroi.Rules.Contributors;

namespace Muonroi.Rules.Tests.Feel;

public sealed class FeelWebExtensionsTests
{
    [Fact]
    public void AddFeelWeb_RegistersManifestContributor_AndControllerAssembly()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddFeelWeb();

        result.Should().BeSameAs(services);

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetServices<IUiEngineManifestContributor>()
            .Should().ContainSingle(x => x is FeelPlaygroundManifestContributor);

        ApplicationPartManager manager = provider.GetRequiredService<ApplicationPartManager>();
        manager.ApplicationParts.Should().Contain(x => x.Name == typeof(FeelController).Assembly.GetName().Name);
    }
}
