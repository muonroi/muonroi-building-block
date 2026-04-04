using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.AspNetCore.OpenApi.OpenApi;

/// <summary>
/// Represents the Swagger/OpenAPI default values for the API explorer.
/// </summary>
/// <param name="jsonSerializeService">The JSON serialization service.</param>
public class SwaggerDefaultValues(IMJsonSerializeService jsonSerializeService) : IOperationFilter
{
    /// <summary>
    /// Applies the filter to the specified operation using the given context.
    /// </summary>
    /// <param name="operation">The operation to apply the filter to.</param>
    /// <param name="context">The filter context.</param>
    public void Apply(OpenApiOperation? operation, OperationFilterContext context)
    {
        Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription apiDescription = context.ApiDescription;

        if (operation == null)
        {
            return;
        }

        foreach (Microsoft.AspNetCore.Mvc.ApiExplorer.ApiResponseType responseType in context.ApiDescription.SupportedResponseTypes)
        {
            string responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString();
            OpenApiResponse response = operation.Responses[responseKey];
            foreach (string? contentType in from string? contentType in response.Content.Keys
                                            where responseType.ApiResponseFormats.All(x => x.MediaType != contentType)
                                            select contentType)
            {
                response.Content.Remove(contentType);
            }
        }

        if (operation.Parameters == null)
        {
            return;
        }

        foreach (OpenApiParameter? parameter in operation.Parameters)
        {
            Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription description = apiDescription.ParameterDescriptions.First(p => p.Name == parameter.Name);

            parameter.Description ??= description.ModelMetadata?.Description;

            if (parameter.Schema.Default == null &&
                description.DefaultValue != null &&
                description.DefaultValue is not DBNull &&
                description.ModelMetadata is { } modelMetadata)
            {
                string json = jsonSerializeService.Serialize(description.DefaultValue); // MBB002-exempt: requires Type-based overload not available in IMJsonSerializeService wrapper
                parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(json);
            }

            parameter.Required |= description.IsRequired;
        }
    }
}
