using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Governance.License;

/// <summary>
/// Exception thrown when license operations fail.
/// </summary>
public sealed class LicenseException : MException
{
    /// <summary>
    /// Initializes a new instance of LicenseException.
    /// </summary>
    public LicenseException()
        : base("LICENSE_ERROR", "License operation failed", MExceptionCategory.Security, 403)
    {
    }

    /// <summary>
    /// Initializes a new instance of LicenseException.
    /// </summary>
    public LicenseException(string message)
        : base("LICENSE_ERROR", message ?? "License operation failed", MExceptionCategory.Security, 403)
    {
    }

    /// <summary>
    /// Initializes a new instance of LicenseException.
    /// </summary>
    public LicenseException(string message, Exception innerException)
        : base("LICENSE_ERROR", message ?? "License operation failed", MExceptionCategory.Security, 403, innerException)
    {
    }
}
