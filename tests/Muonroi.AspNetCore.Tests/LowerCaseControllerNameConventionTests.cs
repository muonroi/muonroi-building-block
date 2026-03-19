namespace Muonroi.AspNetCore.Tests;

public class LowerCaseControllerNameConventionTests
{
    private static string InvokeConvert(string name, string suffix)
    {
        MethodInfo method = typeof(LowerCaseControllerNameConvention)
            .GetMethod("ConvertToLowerCaseExceptSuffix", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [name, suffix])!;
    }

    private sealed class DummyController : ControllerBase
    {
    }

    [Fact]
    public void Convert_With_Suffix_Returns_Expected()
    {
        string result = InvokeConvert("TestController", "Controller");

        result.Should().Be("testController");
    }

    [Fact]
    public void Convert_Empty_Returns_Empty()
    {
        string result = InvokeConvert(string.Empty, "Controller");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Convert_Null_Throws()
    {
        MethodInfo method = typeof(LowerCaseControllerNameConvention)
            .GetMethod("ConvertToLowerCaseExceptSuffix", BindingFlags.NonPublic | BindingFlags.Static)!;

        Action action = () => method.Invoke(null, [null!, "Controller"]);

        action.Should().Throw<TargetInvocationException>();
    }

    [Fact]
    public void Apply_Replaces_Template_With_Lower_Name()
    {
        ControllerModel model = new(typeof(DummyController).GetTypeInfo(), [])
        {
            ControllerName = "Dummy"
        };
        SelectorModel selector = new()
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute("api/[controller]"))
        };
        model.Selectors.Add(selector);

        new LowerCaseControllerNameConvention().Apply(model);

        selector.AttributeRouteModel!.Template.Should().Be("api/dummy");
    }

    [Fact]
    public void Apply_Empty_Name_Uses_Empty_Template()
    {
        ControllerModel model = new(typeof(DummyController).GetTypeInfo(), [])
        {
            ControllerName = string.Empty
        };
        SelectorModel selector = new()
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute("[controller]"))
        };
        model.Selectors.Add(selector);

        new LowerCaseControllerNameConvention().Apply(model);

        model.Selectors[0].AttributeRouteModel!.Template.Should().BeEmpty();
    }
}
