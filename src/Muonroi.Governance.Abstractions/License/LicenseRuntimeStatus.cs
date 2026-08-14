namespace Muonroi.Governance.License;

/// <summary>
/// Mutable runtime state derived from verified license material plus online checks.
/// </summary>
public sealed class LicenseRuntimeStatus
{
    private readonly object _sync = new();
    private bool _degradedToFree;
    private string? _degradeReason;
    private string? _currentHeartbeatNonce;
    private DateTimeOffset? _lastHeartbeatAtUtc;
    private DateTimeOffset? _revocationGraceUntilUtc;

    /// <summary>
    /// The Is Degraded To Free.
    /// </summary>
    public bool IsDegradedToFree
    {
        get
        {
            lock (_sync)
            {
                return _degradedToFree;
            }
        }
    }

    /// <summary>
    /// The Degrade Reason.
    /// </summary>
    public string? DegradeReason
    {
        get
        {
            lock (_sync)
            {
                return _degradeReason;
            }
        }
    }

    /// <summary>
    /// The Current Heartbeat Nonce.
    /// </summary>
    public string? CurrentHeartbeatNonce
    {
        get
        {
            lock (_sync)
            {
                return _currentHeartbeatNonce;
            }
        }
    }

    /// <summary>
    /// The Last Heartbeat At Utc.
    /// </summary>
    public DateTimeOffset? LastHeartbeatAtUtc
    {
        get
        {
            lock (_sync)
            {
                return _lastHeartbeatAtUtc;
            }
        }
    }

    /// <summary>
    /// The Revocation Grace Until Utc.
    /// </summary>
    public DateTimeOffset? RevocationGraceUntilUtc
    {
        get
        {
            lock (_sync)
            {
                return _revocationGraceUntilUtc;
            }
        }
    }

    /// <summary>
    /// Executes the Initialize From Proof operation.
    /// </summary>
    public void InitializeFromProof(ActivationProof? proof)
    {
        if (proof == null)
        {
            return;
        }

        lock (_sync)
        {
            _currentHeartbeatNonce ??= proof.HeartbeatNonce;
        }
    }

    /// <summary>
    /// Executes the Update Heartbeat Success operation.
    /// </summary>
    public void UpdateHeartbeatSuccess(string? newNonce, DateTimeOffset checkedAtUtc)
    {
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(newNonce))
            {
                _currentHeartbeatNonce = newNonce;
            }

            _lastHeartbeatAtUtc = checkedAtUtc;
            _revocationGraceUntilUtc = null;
            _degradedToFree = false;
            _degradeReason = null;
        }
    }

    /// <summary>
    /// Executes the Start Revocation Grace operation.
    /// </summary>
    public void StartRevocationGrace(DateTimeOffset graceUntilUtc)
    {
        lock (_sync)
        {
            _revocationGraceUntilUtc = graceUntilUtc;
            if (DateTimeOffset.UtcNow >= graceUntilUtc)
            {
                _degradedToFree = true;
                _degradeReason = "revoked";
            }
        }
    }

    /// <summary>
    /// Executes the Evaluate Grace Period operation.
    /// </summary>
    public bool EvaluateGracePeriod(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (_degradedToFree || !_revocationGraceUntilUtc.HasValue)
            {
                return _degradedToFree;
            }

            if (nowUtc < _revocationGraceUntilUtc.Value)
            {
                return false;
            }

            _degradedToFree = true;
            _degradeReason = "revoked";
            return true;
        }
    }

    /// <summary>
    /// Executes the Downgrade To Free operation.
    /// </summary>
    public void DowngradeToFree(string reason)
    {
        lock (_sync)
        {
            _degradedToFree = true;
            _degradeReason = reason;
        }
    }

    /// <summary>
    /// Executes the Get Effective Tier operation.
    /// </summary>
    public LicenseTier GetEffectiveTier(LicenseState state)
    {
        MGuard.NotNull(state);
        lock (_sync)
        {
            if (_degradedToFree)
            {
                return LicenseTier.Free;
            }
        }

        return state.ActivationProof?.Tier ?? state.Tier;
    }

    /// <summary>
    /// Executes the Has Feature operation.
    /// </summary>
    public bool HasFeature(LicenseState state, string featureName)
    {
        MGuard.NotNull(state);
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return false;
        }

        lock (_sync)
        {
            if (_degradedToFree)
            {
                return FreeTierFeatures.IsAllowed(featureName);
            }
        }

        return state.HasFeature(featureName);
    }
}
