namespace Muonroi.AspNetCore.Controllers.ActionFilters;

/// <inheritdoc />
public class MControllerBaseConvention : IApplicationModelConvention
{
/// <inheritdoc />
    public void Apply(ApplicationModel application)
    {
        foreach ((ActionModel action, Type returnType) in from ControllerModel controller in application.Controllers
                                                          where typeof(MControllerBase).IsAssignableFrom(controller.ControllerType)
                                                          from ActionModel action in controller.Actions
                                                          let returnType = action.ActionMethod.ReturnType
                                                          select (action, returnType))
        {
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type genericArgument = returnType.GetGenericArguments()[0];

                if (genericArgument.IsGenericType && genericArgument.GetGenericTypeDefinition() == typeof(MResponse<>))
                {
                    Type responseType = genericArgument.GetGenericArguments()[0];
                    action.Filters.Add(new ProducesResponseTypeAttribute(
                        typeof(MResponse<>).MakeGenericType(responseType), (int)HttpStatusCode.OK));
                }
            }

            action.Filters.Add(
                new ProducesResponseTypeAttribute(typeof(MVoidMethodResult), (int)HttpStatusCode.BadRequest));
        }
    }
}
