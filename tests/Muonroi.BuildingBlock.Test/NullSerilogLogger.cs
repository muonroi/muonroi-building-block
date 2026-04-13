namespace Muonroi.BuildingBlock.Test;

public class NullSerilogLogger : ILogger
{
    public IDisposable BeginScope<TState>(TState state)
    {
        return new Mock<IDisposable>().Object;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return false;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }
}
