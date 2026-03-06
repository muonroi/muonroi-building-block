namespace Muonroi.Core.Abstractions.Context;

public interface ILogScopeFactory
{
    IDisposable? BeginScope(IReadOnlyDictionary<string, object?> properties);
}

public sealed class NullLogScopeFactory : ILogScopeFactory
{
    public static readonly NullLogScopeFactory Instance = new();

    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> properties)
    {
        return null;
    }
}
