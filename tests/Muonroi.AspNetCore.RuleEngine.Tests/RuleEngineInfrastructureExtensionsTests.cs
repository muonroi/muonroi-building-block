namespace Muonroi.AspNetCore.RuleEngine.Tests;

public class RuleEngineInfrastructureExtensionsTests
{
    private class TestEntity : MEntity { }

    [Fact]
    public void AddRuleEngineInfrastructure_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddRuleEngineInfrastructure(configuration);

        // Assert
        services.Any(d => d.ServiceType == typeof(IRuleChangeStore)).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(IRuleChangeProposalStore)).Should().BeTrue();
    }

    [Fact]
    public void AddCrudRules_ShouldRegisterOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrudRules<TestEntity>();

        // Assert
        services.Any(d => d.ServiceType == typeof(RuleOrchestrator<CrudContext<TestEntity>>)).Should().BeTrue();
    }
}
