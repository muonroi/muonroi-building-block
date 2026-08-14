using Muonroi.Kubernetes.Kubernetes;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Kubernetes
//
// This is a thin configuration package: it exposes the KubernetesConfigs options
// class (bound from the "KubernetesConfigs" section) and the KubernetesClusterType
// enum (K8S | K3S | Eks). It contains no service-registration extension and no
// middleware — consumers bind the options and read them where cluster awareness is
// needed (e.g. choosing an in-cluster vs. external API endpoint).
//
// We bind KubernetesConfigs into the options system so it can be injected via
// IOptions<KubernetesConfigs>. The app runs with NO external dependency.
// -------------------------------------------------------------------------
builder.Services.Configure<KubernetesConfigs>(
    builder.Configuration.GetSection(KubernetesConfigs.SectionName));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Kubernetes API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Kubernetes: binding KubernetesConfigs (ClusterType + " +
                      "ClusterEndpoint) and the KubernetesClusterType enum (K8S | K3S | Eks)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
