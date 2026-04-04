namespace Muonroi.Governance.License;

/// <summary>
/// Provides a context to track license execution state across asynchronous flows.
/// Used primarily to prevent infinite loops in database interceptors.
/// </summary>
public sealed class LicenseExecutionContext
{
    private static readonly AsyncLocal<bool> _isInLicenseCheck = new();

    /// <summary>
    /// Gets or sets a value indicating whether the current flow is already performing a license check.
    /// </summary>
    public static bool IsInLicenseCheck
    {
        get => _isInLicenseCheck.Value;
        set => _isInLicenseCheck.Value = value;
    }

    /// <summary>
    /// Begins a new license check scope.
    /// </summary>
    /// <returns>An IDisposable that clears the scope when disposed.</returns>
    public static IDisposable BeginScope()
    {
        return new LicenseScope();
    }

    private sealed class LicenseScope : IDisposable
    {
        private readonly bool _originalValue;

        public LicenseScope()
        {
            _originalValue = _isInLicenseCheck.Value;
            _isInLicenseCheck.Value = true;
        }

        public void Dispose()
        {
            _isInLicenseCheck.Value = _originalValue;
        }
    }
}
