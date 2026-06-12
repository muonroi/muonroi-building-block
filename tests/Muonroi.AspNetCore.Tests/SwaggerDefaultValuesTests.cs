namespace Muonroi.AspNetCore.Tests;

public class SwaggerDefaultValuesTests
{
    private static OperationFilterContext CreateContext(ApiDescription apiDescription)
    {
        MethodInfo methodInfo = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
        return new OperationFilterContext(
            apiDescription,
            Substitute.For<ISchemaGenerator>(),
            new SchemaRepository(),
            methodInfo);
    }

    [Fact]
    public void Apply_Sets_Defaults()
    {
        OpenApiOperation operation = new();
        OpenApiParameter parameter = new()
        {
            Name = "p",
            Schema = new OpenApiSchema()
        };
        operation.Parameters = [parameter];
        operation.Responses["200"] = new OpenApiResponse
        {
            Content =
            {
                ["application/json"] = new OpenApiMediaType(),
                ["text/plain"] = new OpenApiMediaType()
            }
        };

        ApiDescription description = new();
        ApiResponseType response = new() { StatusCode = 200 };
        response.ApiResponseFormats.Add(new ApiResponseFormat { MediaType = "application/json" });
        description.SupportedResponseTypes.Add(response);

        EmptyModelMetadataProvider provider = new();
        ApiParameterDescription parameterDescription = new()
        {
            Name = "p",
            ModelMetadata = provider.GetMetadataForType(typeof(string)),
            DefaultValue = "abc",
            IsRequired = true
        };
        description.ParameterDescriptions.Add(parameterDescription);

        SwaggerDefaultValues filter = new(new MJsonSerializeService());
        filter.Apply(operation, CreateContext(description));

        Assert.Single(operation.Responses["200"].Content.Keys);
        Assert.True(operation.Parameters[0].Required);
        Assert.Equal("abc", ((OpenApiString)operation.Parameters[0].Schema.Default).Value);
    }

    [Fact]
    public void Apply_Null_Context_Throws()
    {
        SwaggerDefaultValues filter = new(new MJsonSerializeService());
        Assert.Throws<NullReferenceException>(() => filter.Apply(new OpenApiOperation(), null!));
    }

    [Fact]
    public void Apply_Null_Operation_Does_Not_Throw()
    {
        ApiDescription description = new();
        OperationFilterContext context = CreateContext(description);
        SwaggerDefaultValues filter = new(new MJsonSerializeService());

        Exception? exception = Record.Exception(() => filter.Apply(null, context));
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_With_Different_Default_Types()
    {
        OpenApiOperation operation = new();
        OpenApiParameter stringParameter = new()
        {
            Name = "s",
            Schema = new OpenApiSchema()
        };
        operation.Parameters =
        [
            stringParameter,
            new OpenApiParameter { Name = "i", Schema = new OpenApiSchema() }
        ];

        ApiDescription description = new();
        EmptyModelMetadataProvider provider = new();
        description.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Name = "s",
            ModelMetadata = provider.GetMetadataForType(typeof(string)),
            DefaultValue = "str"
        });
        description.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Name = "i",
            ModelMetadata = provider.GetMetadataForType(typeof(int)),
            DefaultValue = 5
        });

        SwaggerDefaultValues filter = new(new MJsonSerializeService());
        filter.Apply(operation, CreateContext(description));

        Assert.Equal("str", ((OpenApiString)operation.Parameters[0].Schema.Default).Value);
        Assert.Equal(5, ((OpenApiInteger)operation.Parameters[1].Schema.Default).Value);
    }
}
