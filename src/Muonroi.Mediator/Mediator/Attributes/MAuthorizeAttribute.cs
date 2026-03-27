namespace Muonroi.Mediator.Mediator.Attributes;

/// <summary>
/// Declares authorization requirements for a mediator request.
/// Multiple <see cref="MAuthorizeAttribute"/> instances may be stacked on the same request class;
/// ALL constraints must be satisfied (logical AND).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class MAuthorizeAttribute : Attribute
{
    /// <summary>
    /// Comma-separated list of role names. At least one must be present in the execution context.
    /// </summary>
    public string Roles { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of permission strings. ALL must be present in the execution context.
    /// </summary>
    public string Permissions { get; set; } = string.Empty;
}
