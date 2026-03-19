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

    public void DowngradeToFree(string reason)
    {
        lock (_sync)
        {
            _degradedToFree = true;
            _degradeReason = reason;
        }
    }

    public LicenseTier GetEffectiveTier(LicenseState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_sync)
        {
            if (_degradedToFree)
            {
                return LicenseTier.Free;
            }
        }

        return state.ActivationProof?.Tier ?? state.Tier;
    }

    public bool HasFeature(LicenseState state, string featureName)
    {
        ArgumentNullException.ThrowIfNull(state);
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
