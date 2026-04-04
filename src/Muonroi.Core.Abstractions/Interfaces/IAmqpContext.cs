namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Defines the context for AMQP (Advanced Message Queuing Protocol) operations.
/// </summary>
public interface IAmqpContext
{
    /// <summary>
    /// Gets a header value by its key.
    /// </summary>
    /// <param name="key">The key of the header.</param>
    /// <returns>The header value if found; otherwise, null.</returns>
    string? GetHeaderByKey(string key);

    /// <summary>
    /// Adds a collection of headers to the context.
    /// </summary>
    /// <param name="headers">The dictionary of headers to add.</param>
    void AddHeaders(IDictionary<string, object> headers);

    /// <summary>
    /// Clears all headers from the context.
    /// </summary>
    void ClearHeaders();
}
