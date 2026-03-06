namespace Muonroi.BuildingBlock.Test;

public class LowerCaseControllerNameConventionTests
{
    private static string InvokeConvert(string name, string suffix)
    {
        MethodInfo mi = typeof(LowerCaseControllerNameConvention)
            .GetMethod("ConvertToLowerCaseExceptSuffix", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)mi.Invoke(null, [name, suffix])!;
    }

    private class DummyController : ControllerBase
    {
    }

    [Fact]
    public void Convert_With_Suffix_Returns_Expected()
    {
        string result = InvokeConvert("TestController", "Controller");
        Assert.Equal("testController", result);
    }

    [Fact]
    public void Convert_Empty_Returns_Empty()
    {
        string result = InvokeConvert(string.Empty, "Controller");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_Null_Throws()
    {
        MethodInfo mi = typeof(LowerCaseControllerNameConvention)
            .GetMethod("ConvertToLowerCaseExceptSuffix", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Throws<TargetInvocationException>(() => mi.Invoke(null, [null, "Controller"]));
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

        LowerCaseControllerNameConvention conv = new();
        conv.Apply(model);

        Assert.Equal("api/dummy", selector.AttributeRouteModel!.Template);
    }

    [Fact]
    public void Apply_Null_Name_Uses_Empty_Template()
    {
        ControllerModel model = new(typeof(DummyController).GetTypeInfo(), [])
        {
            ControllerName = string.Empty
        };
        SelectorModel item = new()
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute("[controller]"))
        };
        model.Selectors.Add(item);
        LowerCaseControllerNameConvention conv = new();
        conv.Apply(model);
        Assert.Equal(string.Empty, model.Selectors[0].AttributeRouteModel!.Template);
    }
}
