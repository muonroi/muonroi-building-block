using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Muonroi.AspNetCore.Controllers.Conventions;

/// <summary>
/// Applies controller naming rules for generic controller types.
/// </summary>
public class GenericControllerRouteConvention : IControllerModelConvention
{
    /// <summary>
    /// Updates the controller name for generic controllers that map to entity types.
    /// </summary>
    /// <param name="controller">The controller model to update.</param>
    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.IsGenericType &&
            controller.ControllerType.GetGenericTypeDefinition() == typeof(MGenericController<,>))
        {
            Type entityType = controller.ControllerType.GenericTypeArguments[0];
            controller.ControllerName = entityType.Name;
            if (controller.ControllerName.EndsWith("Entity"))
            {
                controller.ControllerName =
                    controller.ControllerName[..^"Entity".Length];
            }
        }
    }
}
