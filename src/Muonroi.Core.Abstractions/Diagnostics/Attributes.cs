namespace Muonroi.Core.Abstractions.Diagnostics;

/// <summary>
/// Marks a method for line-by-line runtime tracing via Roslyn Source Generator.
/// The class containing this method must be 'partial'.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class MTraceableAttribute : Attribute
{
    /// <summary>
    /// Optional name for the trace node. Defaults to method name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Marks a property, parameter, or class as sensitive. 
/// Its value will not be recorded in diagnostic traces.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class MTraceSensitiveAttribute : Attribute { }
