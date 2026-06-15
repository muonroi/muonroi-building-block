using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Observability.OpenTelemetry;
using Muonroi.Logging.Abstractions;
using Quickstart.Observability.Api.Telemetry;

namespace Quickstart.Observability.Api.Controllers;

// ---------------------------------------------------------------------------
// In-memory product catalogue used by the demo — no database required.
// ---------------------------------------------------------------------------
public sealed record Product(int Id, string Name, string Category, decimal Price);

/// <summary>
/// Demonstrates the three pillars of Muonroi.Observability in a single controller:
///
/// 1. <b>Distributed tracing</b> — custom spans via <see cref="ActivitySource.StartActivity"/>,
///    child spans, and tag enrichment (product.id, product.category, tenant.id is added
///    automatically by <c>TenantActivityEnricher</c>).
///
/// 2. <b>Metrics</b> — <see cref="Counter{T}"/> and <see cref="Histogram{T}"/> instruments
///    created on <see cref="ProductsTelemetryDescriptor.ProductsMeter"/>.
///
/// 3. <b>Structured logging</b> — <see cref="IMLog{T}"/> with
///    <see cref="IMLog.BeginProperty"/> scopes that attach request-level properties
///    to every log statement inside the scope.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController(
    IMLog<ProductsController> log,
    ILogger<ProductsController> fallbackLog) : ControllerBase
{
    // -----------------------------------------------------------------------
    // Static catalogue
    // -----------------------------------------------------------------------
    private static readonly List<Product> Catalogue =
    [
        new(1,  "Laptop Pro 15",    "Electronics", 1_299.99m),
        new(2,  "Wireless Mouse",   "Electronics",    29.99m),
        new(3,  "Standing Desk",    "Furniture",     499.00m),
        new(4,  "Ergonomic Chair",  "Furniture",     349.00m),
        new(5,  "USB-C Hub",        "Electronics",    49.99m),
    ];

    // -----------------------------------------------------------------------
    // GET /api/products
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the full product catalogue.
    ///
    /// <b>Tracing:</b> Opens a root span <c>products.list</c>.  The span carries a
    /// <c>products.count</c> tag added after the data is retrieved.
    ///
    /// <b>Metrics:</b> Increments <c>products.requests.count</c> (tagged
    /// <c>endpoint=list</c>) and records <c>products.request.duration</c>.
    ///
    /// <b>Logging:</b> Uses <c>IMLog.BeginProperty</c> to attach
    /// <c>Endpoint=GET /api/products</c> to every log statement in the scope.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        // ---- Metrics: start timing ----------------------------------------
        long startMs = Stopwatch.GetTimestamp();

        // ---- Tracing: open a root span ------------------------------------
        // StartActivity returns null when no listener is attached — always
        // null-check before calling methods on the activity.
        using Activity? activity = ProductsTelemetryDescriptor.Source
            .StartActivity("products.list", ActivityKind.Server);

        // ---- Logging: structured scope ------------------------------------
        using IMLogContextScope logScope = log.BeginProperty("Endpoint", "GET /api/products");

        log.Info("Listing all {Count} products", Catalogue.Count);

        // ---- Business logic ----------------------------------------------
        List<Product> result = [.. Catalogue];

        // ---- Tracing: enrich the span with result metadata ---------------
        activity?.SetTag("products.count", result.Count);
        activity?.SetTag("http.route", "/api/products");

        // ---- Metrics: record the request ---------------------------------
        TagList tags = new() { { "endpoint", "list" } };
        ProductsTelemetryDescriptor.RequestCount.Add(1, tags);

        double elapsedMs = Stopwatch.GetElapsedTime(startMs).TotalMilliseconds;
        ProductsTelemetryDescriptor.RequestDuration.Record(elapsedMs, tags);

        log.Info("Listed {Count} products in {ElapsedMs:F2} ms", result.Count, elapsedMs);

        return Ok(result);
    }

    // -----------------------------------------------------------------------
    // GET /api/products/{id}
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns a single product by identifier.
    ///
    /// <b>Tracing:</b> Opens a root span <c>products.get_by_id</c> and, inside it,
    /// a child span <c>products.lookup</c> that models the database/cache lookup step.
    /// The child span carries <c>product.id</c> and <c>product.category</c> tags.
    /// On a cache miss the span also records a <c>cache.miss</c> event and the
    /// <see cref="ProductsTelemetryDescriptor.CacheMissCount"/> counter is incremented.
    ///
    /// <b>Exception tagging:</b> When the product is not found,
    /// <see cref="MuonroiTraceProcessor.TagException"/> attaches
    /// <c>exception.category</c> and <c>exception.error_code</c> to the span.
    ///
    /// <b>Metrics:</b> Records duration tagged with <c>endpoint=get_by_id</c> and
    /// <c>found=true|false</c>.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(int id)
    {
        long startMs = Stopwatch.GetTimestamp();

        // ---- Root span ----------------------------------------------------
        using Activity? rootActivity = ProductsTelemetryDescriptor.Source
            .StartActivity("products.get_by_id", ActivityKind.Server);
        rootActivity?.SetTag("product.id.requested", id);

        // ---- Logging scope ------------------------------------------------
        using IMLogContextScope logScope = log.BeginProperty("ProductId", id);
        log.Info("Looking up product {ProductId}", id);

        // ---- Child span: simulated lookup ---------------------------------
        // A child span is created by calling StartActivity while a parent
        // activity is already active on the current execution context.
        Product? product;
        using (Activity? lookupActivity = ProductsTelemetryDescriptor.Source
                   .StartActivity("products.lookup", ActivityKind.Internal))
        {
            lookupActivity?.SetTag("lookup.strategy", "in-memory-catalogue");

            // Simulate a cache miss for odd-numbered IDs.
            bool cacheHit = id % 2 == 0;
            lookupActivity?.SetTag("cache.hit", cacheHit);

            if (!cacheHit)
            {
                // Record an event on the span (visible in Jaeger / Zipkin timelines).
                lookupActivity?.AddEvent(new ActivityEvent("cache.miss",
                    tags: new ActivityTagsCollection { { "product.id", id } }));

                ProductsTelemetryDescriptor.CacheMissCount.Add(1,
                    new TagList { { "product.id", id } });

                log.Warn("Cache miss for product {ProductId}; falling back to catalogue", id);
            }

            product = Catalogue.FirstOrDefault(p => p.Id == id);

            if (product is not null)
            {
                // Enrich child span with the resolved entity details.
                lookupActivity?.SetTag("product.id",       product.Id);
                lookupActivity?.SetTag("product.name",     product.Name);
                lookupActivity?.SetTag("product.category", product.Category);
            }
        }

        // ---- Metrics & result --------------------------------------------
        bool found = product is not null;
        double elapsedMs = Stopwatch.GetElapsedTime(startMs).TotalMilliseconds;

        TagList metricTags = new()
        {
            { "endpoint", "get_by_id" },
            { "found",    found.ToString().ToLowerInvariant() }
        };

        ProductsTelemetryDescriptor.RequestCount.Add(1, metricTags);
        ProductsTelemetryDescriptor.RequestDuration.Record(elapsedMs, metricTags);

        if (!found)
        {
            rootActivity?.SetStatus(ActivityStatusCode.Error, $"Product {id} not found");
            rootActivity?.SetTag("http.status_code", 404);

            log.Warn("Product {ProductId} not found ({ElapsedMs:F2} ms)", id, elapsedMs);
            return NotFound(new { message = $"Product with id={id} was not found." });
        }

        rootActivity?.SetTag("product.id",       product!.Id);
        rootActivity?.SetTag("product.category", product.Category);
        rootActivity?.SetTag("http.status_code", 200);

        log.Info("Returned product {ProductId} ({Category}) in {ElapsedMs:F2} ms",
            product.Id, product.Category, elapsedMs);

        return Ok(product);
    }

    // -----------------------------------------------------------------------
    // GET /api/products/error-demo
    // -----------------------------------------------------------------------

    /// <summary>
    /// Intentionally throws so the caller can observe how
    /// <see cref="MuonroiTraceProcessor.TagException"/> enriches the error span.
    /// </summary>
    [HttpGet("error-demo")]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult ErrorDemo()
    {
        using Activity? activity = ProductsTelemetryDescriptor.Source
            .StartActivity("products.error_demo", ActivityKind.Internal);

        try
        {
            throw new InvalidOperationException("Intentional demo error — observe the span in your tracing backend.");
        }
        catch (Exception ex)
        {
            // Tag the span with exception details.  For Muonroi-typed exceptions use
            // MuonroiTraceProcessor.TagException which extracts Category and ErrorCode.
            MuonroiTraceProcessor.TagException(activity, ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            log.Error(ex, "Demo error triggered on {Endpoint}", "/api/products/error-demo");

            return StatusCode(500, new { message = "Demo error — check your tracing backend for the enriched span." });
        }
    }
}
