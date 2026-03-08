namespace Muonroi.Logging.Abstractions;

/// <summary>
/// Provides a mechanism to push properties into the logging context.
/// </summary>
public interface IMLogContext
{
    /// <summary>
    /// Pushes a property key and value into the logging context.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    /// <returns>A scope object that should be disposed to remove the property from the context.</returns>
    IMLogContextScope PushProperty(string key, object? value);

    /// <summary>
    /// Pushes multiple properties into the logging context.
    /// </summary>
    /// <param name="properties">The dictionary of property keys and values.</param>
    /// <returns>A scope object that should be disposed to remove the properties from the context.</returns>
    IMLogContextScope PushProperties(IReadOnlyDictionary<string, object?> properties);
}

/// <summary>
/// Represents a logging context scope that can be disposed.
/// </summary>
public interface IMLogContextScope : IDisposable
{
}
