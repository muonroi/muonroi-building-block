using Muonroi.Core.Abstractions.Exceptions;
using System.Text.RegularExpressions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Tenancy.SiteProfile.Web.Dapper;

/// <summary>
/// Validates that column names returned by ISiteColumnMap are safe SQL identifiers.
/// Prevents SQL injection via malformed column map entries.
/// </summary>
public static partial class ColumnNameValidator
{
    /// <summary>
    /// Pattern: starts with letter or underscore, followed by letters, digits, underscores, or dots.
    /// Dots allow schema-qualified names (e.g., "dbo.COLUMN_NAME").
    /// Max length 128 (SQL Server identifier limit).
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.]{0,127}$", RegexOptions.Compiled)]
    private static partial Regex ValidIdentifierRegex();

    /// <summary>
    /// Returns true if the column name is a valid SQL identifier.
    /// </summary>
    public static bool IsValidIdentifier(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return false;
        return ValidIdentifierRegex().IsMatch(columnName);
    }

    /// <summary>
    /// Validates that a column name is a safe SQL identifier.
    /// Throws InvalidOperationException if the name is malformed.
    /// </summary>
    /// <param name="columnName">The column name to validate.</param>
    /// <param name="propertyName">The property name that mapped to this column (for error context).</param>
    /// <exception cref="InvalidOperationException">Thrown when the column name is not a valid SQL identifier.</exception>
    public static void EnsureValidIdentifier(string? columnName, string propertyName)
    {
        MGuard.State(IsValidIdentifier(columnName),
            $"[SQL-SAFETY] Column name '{columnName}' for property '{propertyName}' " +
            $"is not a valid SQL identifier. Expected pattern: ^[A-Za-z_][A-Za-z0-9_.]*$ (max 128 chars). " +
            $"Check your ISiteColumnMap.Column() implementation.");
    }
}
