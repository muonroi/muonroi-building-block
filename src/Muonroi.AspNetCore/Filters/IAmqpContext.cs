namespace Muonroi.AspNetCore.Filters;

/// <inheritdoc />
public interface IAmqpContext
{
    /// <summary>
    /// Clears all AMQP headers tracked in the context.
    /// </summary>
    void ClearHeaders();

    /// <summary>
    /// Adds AMQP headers to the context.
    /// </summary>
    /// <param name="headers">Headers to add.</param>
    void AddHeaders(IDictionary<string, object> headers);

    /// <summary>
    /// Gets a header value by key.
    /// </summary>
    /// <param name="headerKey">Header key.</param>
    string? GetHeaderByKey(string headerKey);
}
