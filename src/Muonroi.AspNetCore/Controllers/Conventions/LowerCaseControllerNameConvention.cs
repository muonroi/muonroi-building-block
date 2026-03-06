namespace Muonroi.AspNetCore.Controllers.Conventions;

public class LowerCaseControllerNameConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        string controllerName = controller.ControllerName;
        string lowerControllerName = ConvertToLowerCaseExceptSuffix(controllerName, "Controller");

        controller.Selectors
            .Where(selector => selector.AttributeRouteModel != null)
            .ToList()
            .ForEach(selector =>
                selector.AttributeRouteModel!.Template =
                    selector.AttributeRouteModel.Template?.Replace("[controller]", lowerControllerName) ?? string.Empty
            );
    }


    private static string ConvertToLowerCaseExceptSuffix(string name, string suffix)
    {
        if (!name.EndsWith(suffix))
        {
            return name.ToLowerInvariant();
        }

        string prefix = name[..^suffix.Length].ToLowerInvariant();
        return prefix + suffix;
    }
}
