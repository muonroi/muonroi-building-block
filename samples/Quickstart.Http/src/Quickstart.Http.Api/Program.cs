using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Http.Http;
using Muonroi.Logging;
using Quickstart.Http.Api.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Http — typed HTTP client building blocks
// The package ships:
//   - BaseApiService            : abstract base for typed clients; SendAsync() runs a
//                                 request through a Polly ResiliencePipeline and deserializes JSON.
//   - CorrelationIdHandler      : DelegatingHandler that propagates correlation-id + api-key headers.
//   - AuthenticateHeaderHandler : DelegatingHandler that attaches a Bearer token from the auth context.
// There is no AddX() registration extension — handlers are wired through the standard
// AddHttpClient(...).AddHttpMessageHandler<T>() pipeline (the idiomatic ASP.NET way).
// -------------------------------------------------------------------------

// IMLog<T> for BaseApiService / AuthenticateHeaderHandler.
builder.Services.AddLogging(lb => lb.AddMuonroiLogging());

// IAuthenticateInfoContext drives the correlation/api-key/bearer handlers.
builder.Services.AddScoped<IAuthenticateInfoContext>(_ => new MAuthenticateInfoContext(isAuthenticated: false));

// The Muonroi DelegatingHandlers must be registered before attaching them.
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddTransient<AuthenticateHeaderHandler>();

// A typed client over Muonroi.Http BaseApiService, with both handlers in its pipeline.
builder.Services.AddHttpClient(JsonPlaceholderClient.ClientName, client =>
    {
        client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
    })
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddHttpMessageHandler<AuthenticateHeaderHandler>();

builder.Services.AddScoped<JsonPlaceholderClient>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Http API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Http: BaseApiService (resilient typed client over a " +
                      "Polly pipeline) plus CorrelationIdHandler and AuthenticateHeaderHandler."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
