namespace Muonroi.Core.Abstractions.Enums;

/// <summary>
/// Specifies the type of log entry.
/// </summary>
public enum LogType
{
    /// <summary>
    /// Log entry for an exception.
    /// </summary>
    [EnumMember]
    Exception = 1,

    /// <summary>
    /// Log entry for tracing information.
    /// </summary>
    [EnumMember]
    Trace
}
