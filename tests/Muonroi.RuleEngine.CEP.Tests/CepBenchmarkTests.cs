namespace Muonroi.RuleEngine.CEP.Tests;

public class CepBenchmarkTests
{
    [Fact]
    public void Benchmark_ReturnsDurations()
    {
        (TimeSpan stream, TimeSpan batch) = CepBenchmark.Run(100);
        Assert.True(stream > TimeSpan.Zero);
        Assert.True(batch > TimeSpan.Zero);
    }
}