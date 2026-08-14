var builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
// Normally you'd register DefaultConnectorRegistry and actual connectors
builder.Services.AddSingleton<IConnectorRegistry, MockConnectorRegistry>();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Integration.Abstractions API",
        Version = "v1",
        Description = "Demonstrates Integration Abstractions and Connector usage."
    });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();

// Mock registry for demonstration
public class MockConnectorRegistry : IConnectorRegistry
{
    public IServiceTaskConnector? Resolve(string connectorType)
    {
        if (connectorType == "mock-email")
            return new MockEmailConnector();
        return null;
    }

    public IReadOnlyList<ConnectorMetadata> ListAvailable()
    {
        return new List<ConnectorMetadata>
        {
            new ConnectorMetadata
            {
                Type = "mock-email",
                DisplayName = "Mock Email Connector",
                Category = "Communication",
                IconSvg = "<svg></svg>",
                Description = "Sends an email (mocked).",
                RequiresCredentials = true
            }
        };
    }
}

public class MockEmailConnector : IServiceTaskConnector
{
    public Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        // Simulate reading from context
        var to = context.InputFacts.TryGetValue("To", out var toVal) ? toVal.ToString() : "unknown";
        return Task.FromResult(new ConnectorResult { Success = true, Output = new Dictionary<string, object> { { "SentTo", to } } });
    }
}
