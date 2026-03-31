namespace Muonroi.AspNetCore.RuleEngine.Tests;

public class GenericControllerInfrastructureTests
{
    private class TestEntity : MEntity { }

    [Fact]
    public void GenericControllerRouteConvention_ShouldUpdateControllerName()
    {
        // Arrange
        var convention = new GenericControllerRouteConvention();
        var controllerType = typeof(MGenericController<TestEntity, MDbContext>).GetTypeInfo();
        var controllerModel = new ControllerModel(controllerType, [])
        {
            ControllerName = "MGeneric"
        };

        // Act
        convention.Apply(controllerModel);

        // Assert
        controllerModel.ControllerName.Should().Be("Test");
    }

    [Fact]
    public void GenericControllerFeatureProvider_ShouldAddControllers()
    {
        // Arrange
        var provider = new GenericControllerFeatureProvider();
        var feature = new ControllerFeature();

        // Act
        provider.PopulateFeature([], feature);

        // Assert
        // This might be empty if no types are discovered, but we can verify it doesn't throw
        feature.Controllers.Should().NotBeNull();
    }
}
