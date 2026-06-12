namespace Muonroi.BuildingBlock.Test;

public class MControllerBaseConventionTests
{
    private class DummyController() : MControllerBase(null!, null!, null!)
    {
        public static Task<MResponse<string>> Action()
        {
            return Task.FromResult(new MResponse<string>());
        }
    }

    [Fact]
    public void Apply_Adds_Filters()
    {
        ApplicationModel app = new();
        ControllerModel controller = new(typeof(DummyController).GetTypeInfo(), []);
        MethodInfo method = typeof(DummyController).GetMethod(nameof(DummyController.Action))!;
        ActionModel action = new(method, []);
        controller.Actions.Add(action);
        app.Controllers.Add(controller);

        MControllerBaseConvention conv = new();
        conv.Apply(app);

        Assert.Contains(action.Filters,
            f => f is ProducesResponseTypeAttribute { StatusCode: (int)HttpStatusCode.OK } a &&
                 a.Type == typeof(MResponse<string>));
        Assert.Contains(action.Filters,
            f => f is ProducesResponseTypeAttribute { StatusCode: (int)HttpStatusCode.BadRequest } a &&
                 a.Type == typeof(MVoidMethodResult));
    }

    [Fact]
    public void Apply_Null_Throws()
    {
        MControllerBaseConvention conv = new();
        Assert.Throws<NullReferenceException>(() => conv.Apply(null!));
    }
}
