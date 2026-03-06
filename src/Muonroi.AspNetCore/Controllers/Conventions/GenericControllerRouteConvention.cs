namespace Muonroi.AspNetCore.Controllers.Conventions;

public class GenericControllerRouteConvention : IControllerModelConvention
{
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
