namespace Muonroi.BuildingBlock.Test;

public class SwaggerDefaultValuesTests
{
    private static OperationFilterContext CreateContext(ApiDescription apiDesc)
    {
        MethodInfo mi = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
        return new OperationFilterContext(apiDesc, Substitute.For<ISchemaGenerator>(), new SchemaRepository(), mi);
    }

    [Fact]
    public void Apply_Sets_Defaults()
    {
        OpenApiOperation op = new();
        OpenApiParameter parameter = new()
        {
            Name = "p",
            Schema = new OpenApiSchema()
        };
        op.Parameters = [parameter];
        op.Responses["200"] = new OpenApiResponse
        {
            Content = { ["application/json"] = new OpenApiMediaType(), ["text/plain"] = new OpenApiMediaType() }
        };

        ApiDescription desc = new();
        ApiResponseType resp = new()
        {
            StatusCode = 200
        };
        ApiResponseFormat item = new()
        {
            MediaType = "application/json"
        };
        resp.ApiResponseFormats.Add(item);
        desc.SupportedResponseTypes.Add(resp);
        EmptyModelMetadataProvider provider = new();
        ApiParameterDescription param = new()
        {
            Name = "p",
            ModelMetadata = provider.GetMetadataForType(typeof(string)),
            DefaultValue = "abc",
            IsRequired = true
        };
        desc.ParameterDescriptions.Add(param);
        SwaggerDefaultValues filter = new();
        filter.Apply(op, CreateContext(desc));
        Assert.Single(op.Responses["200"].Content.Keys);
        Assert.True(op.Parameters![0].Required);
        Assert.Equal("abc", ((OpenApiString)op.Parameters[0].Schema.Default).Value);
    }

    [Fact]
    public void Apply_Null_Context_Throws()
    {
        SwaggerDefaultValues filter = new();
        Assert.Throws<NullReferenceException>(() => filter.Apply(new OpenApiOperation(), null!));
    }

    [Fact]
    public void Apply_Null_Operation_Does_Not_Throw()
    {
        ApiDescription desc = new();
        OperationFilterContext ctx = CreateContext(desc);
        SwaggerDefaultValues filter = new();
        Exception ex = Record.Exception(() => filter.Apply(null!, ctx));
        Assert.Null(ex);
    }

    [Fact]
    public void Apply_With_Different_Default_Types()
    {
        OpenApiOperation op = new();
        OpenApiParameter parameter = new()
        {
            Name = "s",
            Schema = new OpenApiSchema()
        };
        op.Parameters = [
            parameter,
            new OpenApiParameter { Name = "i", Schema = new OpenApiSchema() }
        ];
        ApiDescription desc = new();
        EmptyModelMetadataProvider provider = new();
        ApiParameterDescription item = new()
        {
            Name = "s",
            ModelMetadata = provider.GetMetadataForType(typeof(string)),
            DefaultValue = "str"
        };
        desc.ParameterDescriptions.Add(item);
        ApiParameterDescription description = new()
        {
            Name = "i",
            ModelMetadata = provider.GetMetadataForType(typeof(int)),
            DefaultValue = 5
        };
        desc.ParameterDescriptions.Add(description);
        SwaggerDefaultValues filter = new();
        filter.Apply(op, CreateContext(desc));
        Assert.Equal("str", ((OpenApiString)op.Parameters![0].Schema.Default).Value);
        Assert.Equal(5, ((OpenApiInteger)op.Parameters[1].Schema.Default).Value);
    }
}
