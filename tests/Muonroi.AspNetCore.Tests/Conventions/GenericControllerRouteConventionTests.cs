namespace Muonroi.AspNetCore.Tests.Conventions;

public class GenericControllerRouteConventionTests
{
    [Fact]
    public void Apply_NonGenericController_DoesNotModifyName()
    {
        var convention = new GenericControllerRouteConvention();
        var controllerType = typeof(MControllerBase).GetTypeInfo();
        var controller = new ControllerModel(controllerType, []);

        string originalName = controller.ControllerName;
        convention.Apply(controller);

        controller.ControllerName.Should().Be(originalName);
    }
}
