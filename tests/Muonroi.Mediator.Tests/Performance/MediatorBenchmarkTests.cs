using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Mediator.Mediator;
using System.Diagnostics;

namespace Muonroi.Mediator.Tests.Performance;

public class MediatorBenchmarkTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public MediatorBenchmarkTests()
    {
        var services = new ServiceCollection();
        services.AddMMediator(options => {
            options.Assemblies = [typeof(MediatorBenchmarkTests).Assembly];
        });
        
        // Mock a handler
        services.AddTransient<IRequestHandler<PingRequest, PongResponse>, PingHandler>();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task RequestHandlerWrapperCache_ShouldBe_ThreadSafe()
    {
        // Arrange
        const int iterations = 1000;
        var tasks = new List<Task<PongResponse>>();

        // Act & Assert (Should not throw exception during concurrent cache population)
        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(_mediator.Send(new PingRequest { Message = $"Ping {i}" }));
        }

        var results = await Task.WhenAll(tasks);

        results.Length.Should().Be(iterations);
        results[0].Reply.Should().StartWith("Pong");
    }

    [Fact]
    public async Task Mediator_WarmUp_Performance_ShouldBe_Fast()
    {
        // Arrange
        var request = new PingRequest { Message = "Benchmark" };
        
        // Warm up (Populate cache)
        await _mediator.Send(request);

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            await _mediator.Send(request);
        }
        sw.Stop();

        // Output results (Informational)
        Console.WriteLine($"[BENCHMARK] 10,000 calls took: {sw.ElapsedMilliseconds}ms (Avg: {sw.Elapsed.TotalMicroseconds / 10000:F3} µs/call)");
        
        // Verification: average call should be very fast (usually sub-microsecond in local memory)
        sw.ElapsedMilliseconds.Should().BeLessThan(1000); // 10k calls should easily be < 1s
    }

    // --- Mocks ---
    public class PingRequest : IRequest<PongResponse> { public string Message { get; set; } = ""; }
    public class PongResponse { public string Reply { get; set; } = ""; }
    public class PingHandler : IRequestHandler<PingRequest, PongResponse>
    {
        public Task<PongResponse> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PongResponse { Reply = $"Pong {request.Message}" });
        }
    }
}
