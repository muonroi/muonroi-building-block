namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Defines categories for exceptions to help with classification and handling.
/// </summary>
public enum MExceptionCategory
{
    /// <summary>
    /// Business logic or domain-specific rule violations.
    /// </summary>
    Domain = 0,

    /// <summary>
    /// Failures in external systems, databases, or infrastructure components.
    /// </summary>
    Infrastructure = 1,

    /// <summary>
    /// Input data validation failures.
    /// </summary>
    Validation = 2,

    /// <summary>
    /// Security-related failures such as authentication or authorization.
    /// </summary>
    Security = 3
}
