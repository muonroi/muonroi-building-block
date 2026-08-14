namespace Muonroi.RuleGen.Mcp.Infrastructure;

public sealed class ConsoleIsolationRunner
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        StringWriter sink = new(new StringBuilder());

        try
        {
            Console.SetOut(sink);
            Console.SetError(sink);
            return await action();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            await sink.DisposeAsync();
            Gate.Release();
        }
    }
}
