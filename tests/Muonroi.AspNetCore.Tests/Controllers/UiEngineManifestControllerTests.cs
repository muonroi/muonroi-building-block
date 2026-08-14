namespace Muonroi.AspNetCore.Tests.Controllers;

public class UiEngineManifestControllerTests
{
    private readonly ICatalogScanService _catalogScanService;
    private readonly UiEngineManifestController _controller;

    public UiEngineManifestControllerTests()
    {
        _catalogScanService = Substitute.For<ICatalogScanService>();
        _controller = new UiEngineManifestController(_catalogScanService);
    }

    [Fact]
    public void Validate_ValidManifest_ReturnsOk()
    {
        var manifest = new MUiEngineManifest
        {
            SchemaVersion = "mui.engine.v2",
            AppShell = new MUiEngineAppShell { RootLayout = "layout" },
            AuthProfile = new MUiEngineAuthProfile { TokenSource = "header", FailurePolicy = "401" }
        };

        var result = _controller.Validate(manifest);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MUiEngineManifestValidationResponse>(okResult.Value);
        Assert.True(response.IsValid);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Validate_InvalidManifest_ReturnsErrors()
    {
        var manifest = new MUiEngineManifest
        {
            SchemaVersion = "invalid"
        };

        var result = _controller.Validate(manifest);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MUiEngineManifestValidationResponse>(okResult.Value);
        Assert.False(response.IsValid);
        Assert.NotEmpty(response.Errors);
    }

    [Fact]
    public async Task GeneratePrompt_ReturnsOk()
    {
        var request = new MUiEngineGeneratePromptRequest
        {
            Manifest = new MUiEngineManifest(),
            CatalogApis = new List<MUiEngineCatalogApiDescriptor>(),
            CatalogRules = new List<MUiEngineCatalogRuleDescriptor>()
        };
        _catalogScanService.BuildBindingsAsync(Arg.Any<CancellationToken>()).Returns(new List<MUiEngineCatalogBinding>());

        var result = await _controller.GeneratePrompt(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MUiEngineManifestPromptResponse>(okResult.Value);
        Assert.NotEmpty(response.Prompt);
        Assert.NotEmpty(response.MissingFields);
    }
}
