using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Muonroi.Core.Abstractions.Response;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Muonroi.AspNetCore.OpenApi;

/// <summary>
/// Swagger operation filter that automatically adds MErrorResponse documentation to all endpoints.
/// </summary>
public class MErrorResponseFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add 400 Bad Request
        if (!operation.Responses.ContainsKey("400"))
        {
            operation.Responses.Add("400", new OpenApiResponse
            {
                Description = "Bad Request - Validation or Domain logic error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(typeof(MErrorResponse), context.SchemaRepository)
                    }
                }
            });
        }

        // Add 500 Internal Server Error
        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse
            {
                Description = "Internal Server Error - Unhandled exception",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(typeof(MErrorResponse), context.SchemaRepository)
                    }
                }
            });
        }
    }
}
