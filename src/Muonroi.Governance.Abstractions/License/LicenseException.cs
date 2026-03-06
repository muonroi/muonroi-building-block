namespace Muonroi.Governance.License;

/// <summary>
/// Exception thrown when license operations fail.
/// </summary>
public sealed class LicenseException : Exception
{
    public LicenseException()
    {
    }

    public LicenseException(string message) : base(message)
    {
    }

    public LicenseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
