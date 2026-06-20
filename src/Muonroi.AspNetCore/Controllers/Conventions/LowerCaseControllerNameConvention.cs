namespace Muonroi.AspNetCore.Controllers.Conventions;

/// <inheritdoc />
public class LowerCaseControllerNameConvention : IControllerModelConvention
{
/// <inheritdoc />
    public void Apply(ControllerModel controller)
    {
        string controllerName = controller.ControllerName;
        string lowerControllerName = ConvertToLowerCaseExceptSuffix(controllerName, "Controller");

        controller.Selectors
            .Where(selector => selector.AttributeRouteModel != null)
            .ToList()
            .ForEach(selector =>
            {
                if (selector.AttributeRouteModel is { } arm)
                {
                    arm.Template = arm.Template?.Replace("[controller]", lowerControllerName) ?? string.Empty;
                }
            });
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
