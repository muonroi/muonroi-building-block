namespace Muonroi.Governance.License;

/// <summary>
/// Exception thrown when license operations fail.
/// </summary>
public sealed class LicenseException : Exception
{
    /// <summary>
    /// Initializes a new instance of LicenseException.
    /// </summary>
    public LicenseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of LicenseException.
    /// </summary>
    public LicenseException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of LicenseException.
    /// </summary>
    public LicenseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
