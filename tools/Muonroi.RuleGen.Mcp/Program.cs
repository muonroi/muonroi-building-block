using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Helpers;
using Muonroi.RuleGen.Mcp.Infrastructure;

namespace Muonroi.RuleGen.Mcp;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services =>
            {
                services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
                services.AddSingleton<IMDateTimeService, MDateTimeService>();
                services.AddSingleton<ComplianceScanner>();
                services.AddSingleton<ConsoleIsolationRunner>();

                services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly(typeof(Program).Assembly)
                    .WithResourcesFromAssembly(typeof(Program).Assembly)
                    .WithPromptsFromAssembly(typeof(Program).Assembly);
            })
            .Build();

        await host.RunAsync();
    }
}
