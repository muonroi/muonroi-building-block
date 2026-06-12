namespace Muonroi.Core.Abstractions.Diagnostics;

/// <summary>
/// Configuration POCO for the MCausalChain cross-service error propagation feature.
/// Register via <c>services.Configure&lt;MCausalChainOptions&gt;()</c>.
/// </summary>
public sealed class MCausalChainOptions
{
    /// <summary>
    /// Gets or sets the name of the current service.
    /// Used to identify the origin of errors in the causal chain.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether causal chain data is propagated in outgoing HTTP headers.
    /// When <see langword="true"/>, <c>MCausalChainDelegatingHandler</c> injects the chain into
    /// W3C baggage (key: <c>muonroi.causal</c>) on outgoing requests.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool PropagateInHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed depth of the causal chain.
    /// Chains exceeding this depth are rejected at deserialization.
    /// Defaults to 10 per D-03.
    /// </summary>
    public int MaxChainDepth { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether the exception message is included in the propagated chain.
    /// Defaults to <see langword="false"/> per D-12 (avoid PII/sensitive data leakage).
    /// </summary>
    public bool IncludeMessageInChain { get; set; }
}
