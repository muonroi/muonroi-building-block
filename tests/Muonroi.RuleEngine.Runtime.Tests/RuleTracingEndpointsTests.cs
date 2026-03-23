using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Core.Tracing;
using Muonroi.RuleEngine.Runtime.Tracing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleTracingEndpointsTests
{
    [Fact]
    public void MapRuleTracingEndpoints_NullBuilder_Throws()
    {
        Action action = () => RuleTracingEndpoints.MapRuleTracingEndpoints(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EnableEndpoint_UsesDefaultDuration_WhenBodyMissingOrInvalid()
    {
        TestRuleDebuggerModeService service = new();
        await using WebApplication app = await CreateAppAsync(services => services.AddSingleton<IRuleDebuggerModeService>(service));

        HttpResponseMessage response = await app.GetTestClient()
            .PostAsJsonAsync("/muonroi/rule-debugger/tenant-a/enable", new { durationMinutes = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        service.EnableCalls.Should().ContainSingle();
        service.EnableCalls[0].TenantId.Should().Be("tenant-a");
        service.EnableCalls[0].Duration.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task DisableEndpoint_DisablesDebuggerForTenant()
    {
        TestRuleDebuggerModeService service = new();
        await using WebApplication app = await CreateAppAsync(services => services.AddSingleton<IRuleDebuggerModeService>(service));

        HttpResponseMessage response = await app.GetTestClient()
            .PostAsync("/muonroi/rule-debugger/tenant-b/disable", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        service.DisabledTenants.Should().ContainSingle().Which.Should().Be("tenant-b");
    }

    [Fact]
    public async Task TracesEndpoint_ReturnsStorePayload()
    {
        TestRuleTraceStore store = new();
        RuleTraceEntry expected = new()
        {
            TenantId = "tenant-c",
            CorrelationId = "corr-1",
            RuleName = "RULE_1",
            RuleSetVersion = "v2",
            Phase = RuleTracePhase.AfterEval,
            IsSuccess = true
        };
        store.Entries.Add(expected);

        await using WebApplication app = await CreateAppAsync(services => services.AddSingleton<IRuleTraceStore>(store));

        IReadOnlyList<RuleTraceEntry>? payload = await app.GetTestClient()
            .GetFromJsonAsync<IReadOnlyList<RuleTraceEntry>>("/muonroi/rule-debugger/tenant-c/traces?correlationId=corr-1");

        payload.Should().ContainSingle().Which.RuleName.Should().Be("RULE_1");
        store.QueryCalls.Should().ContainSingle();
        store.QueryCalls[0].TenantId.Should().Be("tenant-c");
        store.QueryCalls[0].CorrelationId.Should().Be("corr-1");
    }

    private static async Task<WebApplication> CreateAppAsync(Action<IServiceCollection> configureServices)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        configureServices(builder.Services);

        WebApplication app = builder.Build();
        app.MapRuleTracingEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed class TestRuleDebuggerModeService : IRuleDebuggerModeService
    {
        public List<(string TenantId, TimeSpan Duration)> EnableCalls { get; } = [];
        public List<string> DisabledTenants { get; } = [];

        public ValueTask<bool> IsDebugEnabledAsync(string tenantId, CancellationToken ct = default) => ValueTask.FromResult(false);

        public ValueTask EnableAsync(string tenantId, TimeSpan duration, CancellationToken ct = default)
        {
            EnableCalls.Add((tenantId, duration));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisableAsync(string tenantId, CancellationToken ct = default)
        {
            DisabledTenants.Add(tenantId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestRuleTraceStore : IRuleTraceStore
    {
        public List<RuleTraceEntry> Entries { get; } = [];
        public List<(string TenantId, string? CorrelationId, DateTimeOffset? From)> QueryCalls { get; } = [];

        public ValueTask SaveAsync(RuleTraceEntry entry, TimeSpan ttl, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<RuleTraceEntry>> QueryAsync(string tenantId, string? correlationId, DateTimeOffset? from, CancellationToken ct = default)
        {
            QueryCalls.Add((tenantId, correlationId, from));
            return ValueTask.FromResult<IReadOnlyList<RuleTraceEntry>>(Entries);
        }
    }
}
