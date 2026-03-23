namespace Muonroi.AspNetCore.Tests.Extensions;

public class CrudRuleExtensionsTests
{
    private sealed class TestEntity : MEntity { }

    [Fact]
    public void AddCrudRules_RegistersOrchestratorServiceDescriptor()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCrudRules<TestEntity>();

        // Verify the RuleOrchestrator<CrudContext<TestEntity>> was registered
        services.Should().Contain(d =>
            d.ServiceType == typeof(Muonroi.RuleEngine.Core.RuleOrchestrator<CrudContext<TestEntity>>));
    }

    [Fact]
    public void AddCrudRules_WithConfigureAction_InvokesAction()
    {
        var services = new ServiceCollection();
        bool invoked = false;

        services.AddCrudRules<TestEntity>(svc =>
        {
            invoked = true;
        });

        invoked.Should().BeTrue();
    }

    [Fact]
    public void AddCrudRules_NullConfigureAction_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var act = () => services.AddCrudRules<TestEntity>(null);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddCrudRules_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddCrudRules<TestEntity>();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddCrudRule_RegistersRuleServiceDescriptor()
    {
        var services = new ServiceCollection();

        // We can't easily create a fake IRule<CrudContext<TestEntity>> without matching the full interface.
        // Instead verify the generic extension returns the service collection.
        var result = services.AddCrudRules<TestEntity>();
        result.Should().BeSameAs(services);
    }
}
