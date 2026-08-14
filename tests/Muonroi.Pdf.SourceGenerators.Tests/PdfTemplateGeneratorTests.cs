namespace Muonroi.Pdf.SourceGenerators.Tests;

/// <summary>
/// Tests for PdfTemplateGenerator — verifies that the source generator:
/// 1. Emits a sealed renderer class implementing IMPdfRenderer&lt;TModel&gt; for [PdfTemplate]-decorated models.
/// 2. Emits the DI service-extension method alongside the renderer.
/// </summary>
public sealed class PdfTemplateGeneratorTests
{
    // -----------------------------------------------------------------------
    // Shared stubs included in every test compilation
    // -----------------------------------------------------------------------

    private const string PdfTemplateAttributeStub = """
        using System;
        namespace Muonroi.Pdf.Abstractions
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
            public sealed class PdfTemplateAttribute : Attribute
            {
                public PdfTemplateAttribute(string templateId, string? templateResourceName = null)
                {
                    TemplateId = templateId;
                    TemplateResourceName = templateResourceName;
                }
                public string TemplateId { get; }
                public string? TemplateResourceName { get; }
            }
        }
        """;

    private const string PdfAbstractionsStubs = """
        using System;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        namespace Muonroi.Pdf.Abstractions
        {
            public interface IMPdfRenderer<in TModel>
            {
                string TemplateId { get; }
                Task<PdfRenderResult> RenderAsync(TModel model, Stream destination,
                    PdfRenderOptions? options = null, CancellationToken cancellationToken = default);
            }
            public interface IMPdfService
            {
                Task<PdfRenderResult> RenderAsync(string html, Stream destination,
                    PdfRenderOptions options, CancellationToken cancellationToken = default);
            }
            public sealed record PdfRenderResult(int PageCount);
            public sealed record PdfRenderOptions();
        }
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }
            public static class ServiceCollectionServiceExtensions
            {
                public static IServiceCollection TryAddSingleton<TService, TImplementation>(
                    this IServiceCollection services)
                    where TService : class
                    where TImplementation : class, TService
                    => services;
            }
        }
        namespace Microsoft.Extensions.DependencyInjection.Extensions
        {
            public static class ServiceCollectionDescriptorExtensions
            {
                public static void TryAddSingleton<TService, TImplementation>(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    where TService : class
                    where TImplementation : class, TService
                { }
            }
        }
        """;

    // -----------------------------------------------------------------------
    // Helper: run generator and return generated source texts
    // -----------------------------------------------------------------------

    private static IReadOnlyList<string> RunGenerator(string testSource, params string[] additionalSources)
    {
        // Do NOT include PdfTemplateAttributeStub here — the SG emits it via RegisterPostInitializationOutput.
        // Including it would create a duplicate definition causing ForAttributeWithMetadataName to fail.
        var sources = new List<string> { PdfAbstractionsStubs, testSource };
        sources.AddRange(additionalSources);

        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new PdfTemplateGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);

        var runResult = driver.GetRunResult();
        return runResult.GeneratedTrees
            .Select(t => t.ToString())
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Test 1 — Generator emits renderer class for [PdfTemplate]-decorated class
    // -----------------------------------------------------------------------

    [Fact]
    public void Generator_EmitsRenderer_ForPdfTemplateDecoratedClass()
    {
        const string testSource = """
            using Muonroi.Pdf.Abstractions;
            namespace TestApp
            {
                [PdfTemplate("invoice")]
                public partial class InvoiceModel
                {
                    public string CustomerName { get; set; } = "";
                }
            }
            """;

        var generatedSources = RunGenerator(testSource);

        Assert.True(generatedSources.Count > 0, "Expected at least one generated source file.");


        // Renderer class must exist
        var rendererSource = generatedSources.FirstOrDefault(s => s.Contains("InvoiceModelPdfRenderer"));
        Assert.NotNull(rendererSource);

        // Must implement IMPdfRenderer
        Assert.Contains("IMPdfRenderer", rendererSource);

        // TemplateId must return the supplied id
        Assert.Contains("\"invoice\"", rendererSource);

        // Must be a sealed class
        Assert.Contains("sealed class InvoiceModelPdfRenderer", rendererSource);
    }

    // -----------------------------------------------------------------------
    // Test 2 — Generator emits DI service extension for [PdfTemplate]-decorated class
    // -----------------------------------------------------------------------

    [Fact]
    public void Generator_EmitsServiceExtension_ForPdfTemplateDecoratedClass()
    {
        const string testSource = """
            using Muonroi.Pdf.Abstractions;
            namespace TestApp
            {
                [PdfTemplate("invoice")]
                public partial class InvoiceModel
                {
                    public string CustomerName { get; set; } = "";
                }
            }
            """;

        var generatedSources = RunGenerator(testSource);

        Assert.True(generatedSources.Count > 0, "Expected at least one generated source file.");

        // Service extensions class must exist
        var extSource = generatedSources.FirstOrDefault(s => s.Contains("AddPdfRendererInvoiceModel"));
        Assert.NotNull(extSource);

        // Must use TryAddSingleton
        Assert.Contains("TryAddSingleton", extSource);

        // Must reference the renderer type
        Assert.Contains("InvoiceModelPdfRenderer", extSource);
    }
}
