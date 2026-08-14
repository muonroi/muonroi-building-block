using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
builder.Services.AddSingleton<IExperienceStore, Muonroi.Experience.Runtime.FileExperienceStore>();
builder.Services.AddSingleton<IExperienceBrain, MockExperienceBrain>();
builder.Services.AddSingleton<Muonroi.Experience.Runtime.Extraction.MistakeDetector>();

builder.Services.Configure<ExperienceStoreOptions>(opts =>
{
    opts.StoreType = ExperienceStoreType.File;
    // ensure this path exists or the store will create it
    opts.FileSystemPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "experiences");
});
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data", "experiences"));

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Experience API",
        Version = "v1",
        Description = "Demonstrates Experience Engine, FileStore, and MistakeDetector."
    });
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();

// Mock Brain
public class MockExperienceBrain : IExperienceBrain
{
    public Task<IEnumerable<NeuronExperience>> ExtractAsync(string sessionLog, CancellationToken ct = default)
    {
        var exp = new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = "Extract triggered",
            Question = "How to run?",
            Reasoning = new[] { "Step 1", "Step 2" },
            Solution = "Run the code",
            Confidence = 0.9f,
            Tier = ExperienceTier.SelfQA,
            CreatedFrom = "mock-brain",
            CreatedAt = DateTimeOffset.UtcNow
        };
        return Task.FromResult<IEnumerable<NeuronExperience>>(new[] { exp });
    }

    public Task<NeuronExperience> AbstractAsync(string abstractionPrompt, CancellationToken ct = default)
    {
        return Task.FromResult(new NeuronExperience
        {
            Id = Guid.NewGuid().ToString(),
            Trigger = "Abstracted trigger",
            Question = "Abstracted question",
            Reasoning = new[] { "Abstraction reasoning" },
            Solution = "Abstracted solution",
            Confidence = 1.0f,
            Tier = ExperienceTier.Principle,
            CreatedFrom = "mock-brain-abstract",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
