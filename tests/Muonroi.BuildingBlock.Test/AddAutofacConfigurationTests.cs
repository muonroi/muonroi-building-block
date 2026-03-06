namespace Muonroi.BuildingBlock.Test;

public class AddAutofacConfigurationTests
{
    [Fact]
    public void AddAutofacConfiguration_Uses_AutofacServiceProvider()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddAutofacConfiguration();
        WebApplication app = builder.Build();

        AutofacServiceProvider? provider = app.Services as AutofacServiceProvider;
        Assert.NotNull(provider);
        Assert.Equal("Autofac.Extensions.DependencyInjection.AutofacServiceProvider", provider!.GetType().FullName);
    }

    [Fact]
    public void AddAutofacConfiguration_Multiple_Calls_Register_Modules_Multiple_Times()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddAutofacConfiguration();
        builder.AddAutofacConfiguration();
        WebApplication app = builder.Build();
        AutofacServiceProvider provider = (AutofacServiceProvider)app.Services;
        int count = provider.LifetimeScope.ComponentRegistry
            .RegistrationsFor(new TypedService(typeof(IAmqpContext))).Count();
        Assert.Equal(2, count);
    }
}
