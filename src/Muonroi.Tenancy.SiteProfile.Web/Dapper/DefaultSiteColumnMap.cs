using System.Text;

namespace Muonroi.Tenancy.SiteProfile.Web.Dapper;

/// <summary>
/// Default ISiteColumnMap implementation that converts PascalCase C# property names
/// to UPPER_SNAKE_CASE database column names (the standard convention used across
/// Muonroi site databases).
///
/// Sites that differ only in a few column names subclass this and override just those columns:
/// <code>
/// public class MySiteColumnMap : DefaultSiteColumnMap
/// {
///     public override string Column(string propertyName)
///         => propertyName == "PropertyName" ? "COLUMN_NAME" : base.Column(propertyName);
/// }
/// </code>
/// </summary>
public class DefaultSiteColumnMap : ISiteColumnMap
{
    /// <inheritdoc />
    public virtual string Column(string propertyName)
        => ToUpperSnakeCase(propertyName);

    /// <summary>
    /// Converts a PascalCase string to UPPER_SNAKE_CASE.
    /// Examples: "MyProperty" → "MY_PROPERTY", "Id" → "ID", "MyEntity" → "MY_ENTITY".
    /// </summary>
    protected static string ToUpperSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var sb = new StringBuilder(pascalCase.Length + 4);

        for (int i = 0; i < pascalCase.Length; i++)
        {
            char c = pascalCase[i];

            if (i > 0 && char.IsUpper(c))
            {
                // Insert underscore before uppercase letters that follow:
                // - a lowercase letter (e.g., "No" → "N_o" doesn't apply here)
                // - or before an uppercase letter that is followed by a lowercase (acronym boundary)
                bool prevIsLower = char.IsLower(pascalCase[i - 1]);
                bool nextIsLower = i + 1 < pascalCase.Length && char.IsLower(pascalCase[i + 1]);

                if (prevIsLower || (nextIsLower && i > 0 && char.IsUpper(pascalCase[i - 1])))
                {
                    sb.Append('_');
                }
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }
}
