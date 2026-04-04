namespace Muonroi.Core.Abstractions.Ecosystem;

/// <summary>
/// AOT-safe, thread-safe ecosystem capability registry. Registered as singleton.
/// Uses a volatile field for lock-free reads and a lock for writes.
/// </summary>
public sealed class MEcosystemRegistry : IMEcosystemRegistry
{
    private volatile MCapability _active;

    /// <inheritdoc />
    public MCapability Active => _active;

    /// <inheritdoc />
    public void Register(MCapability capability)
    {
        lock (this)
        {
            _active |= capability;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// No lock required — volatile read ensures the most recently written value
    /// is visible. The bitwise AND is a pure computation with no side effects.
    /// </remarks>
    public bool Has(MCapability capability)
    {
        return (_active & capability) == capability;
    }
}
