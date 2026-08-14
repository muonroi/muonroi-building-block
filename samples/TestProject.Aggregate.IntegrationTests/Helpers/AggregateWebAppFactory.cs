namespace TestProject.Aggregate.IntegrationTests.Helpers;

/// <summary>
/// WebApplicationFactory that hosts TestProject.Aggregate.Host in-process for gRPC integration tests.
///
/// gRPC requires HTTP/2. The test server is configured with HTTP/2 support via AppContext switch
/// so gRPC calls work over plain HTTP (no TLS required in test environment).
/// </summary>
public sealed class AggregateWebAppFactory : WebApplicationFactory<Program>
{
    static AggregateWebAppFactory()
    {
        // Enable HTTP/2 over unencrypted connections for gRPC test client
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Configure Kestrel to use HTTP/2 (required for gRPC)
        builder.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(0, listenOptions =>
            {
                listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }

    /// <summary>
    /// Creates a GrpcChannel connected to the in-process test server.
    /// The channel uses the TestServer's HttpHandler so all traffic stays in-process.
    /// </summary>
    public GrpcChannel CreateGrpcChannel()
    {
        HttpMessageHandler handler = Server.CreateHandler();
        return GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = handler,
            // Suppress TLS verification for in-process test server
            DisposeHttpClient = true
        });
    }
}
