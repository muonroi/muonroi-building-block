namespace Muonroi.Core.Abstractions.Context;

/// <summary>
/// Defines a factory for creating logging scopes.
/// </summary>
public interface ILogScopeFactory
{
    /// <summary>
    /// Begins a new logging scope with the specified properties.
    /// </summary>
    /// <param name="properties">The properties to include in the scope.</param>
    /// <returns>An <see cref="IDisposable"/> that ends the scope when disposed, or null if a scope could not be created.</returns>
    IDisposable? BeginScope(IReadOnlyDictionary<string, object?> properties);
}

/// <summary>
/// A null implementation of <see cref="ILogScopeFactory"/> that does not create any scopes.
/// </summary>
public sealed class NullLogScopeFactory : ILogScopeFactory
{
    /// <summary>
    /// A static instance of <see cref="NullLogScopeFactory"/>.
    /// </summary>
    public static readonly NullLogScopeFactory Instance = new();

    /// <summary>
    /// Begins a new logging scope. Always returns null.
    /// </summary>
    /// <param name="properties">The properties to include in the scope.</param>
    /// <returns>Null.</returns>
    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> properties)
    {
        return null;
    }
}
