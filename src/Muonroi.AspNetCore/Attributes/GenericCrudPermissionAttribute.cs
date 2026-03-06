namespace Muonroi.AspNetCore.Attributes;

/// <summary>
/// Configures permission requirements for Generic CRUD Controller operations.
/// Apply to derived controllers to enable permission-based authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class GenericCrudPermissionAttribute : Attribute
{
    /// <summary>
    /// Permission key required for Read operations (Get, GetById).
    /// If null, uses default pattern: "{EntityName}.View"
    /// </summary>
    public string? ReadPermission { get; set; }

    /// <summary>
    /// Permission key required for Create operations.
    /// If null, uses default pattern: "{EntityName}.Create"
    /// </summary>
    public string? CreatePermission { get; set; }

    /// <summary>
    /// Permission key required for Update operations.
    /// If null, uses default pattern: "{EntityName}.Update"
    /// </summary>
    public string? UpdatePermission { get; set; }

    /// <summary>
    /// Permission key required for Delete operations.
    /// If null, uses default pattern: "{EntityName}.Delete"
    /// </summary>
    public string? DeletePermission { get; set; }

    /// <summary>
    /// Base permission prefix. If set, overrides the default entity name pattern.
    /// Example: "Products" will generate "Products.View", "Products.Create", etc.
    /// </summary>
    public string? PermissionPrefix { get; set; }

    /// <summary>
    /// If true, skips permission checks entirely. Default is false.
    /// Use with caution - only for public/admin endpoints.
    /// </summary>
    public bool SkipPermissionCheck { get; set; } = false;
}
