using System.Text;

namespace Muonroi.Core.Abstractions.Helpers;

/// <summary>
/// Single source of truth for PascalCase → UPPER_SNAKE_CASE conversion.
/// Used by both EF Core (<see cref="SiteColumnExtensions"/>) and Dapper (<see cref="DefaultSiteColumnMap"/>)
/// to guarantee identical column name derivation across all data access layers.
/// <para>
/// <b>Acronym handling:</b> Consecutive uppercase letters are treated as an acronym.
/// The underscore is inserted at the boundary where the acronym ends (before the next lowercase letter).
/// </para>
/// <list type="table">
///   <listheader><term>Input</term><description>Output</description></listheader>
///   <item><term>BookingNo</term><description>BOOKING_NO</description></item>
///   <item><term>ContainerNumber</term><description>CONTAINER_NUMBER</description></item>
///   <item><term>Id</term><description>ID</description></item>
///   <item><term>HTTPServer</term><description>HTTP_SERVER</description></item>
///   <item><term>XMLHttpRequest</term><description>XML_HTTP_REQUEST</description></item>
///   <item><term>URLPath</term><description>URL_PATH</description></item>
///   <item><term>IOControl</term><description>IO_CONTROL</description></item>
///   <item><term>IsHTTPSEnabled</term><description>IS_HTTPS_ENABLED</description></item>
/// </list>
/// </summary>
public static class ColumnNamingConvention
{
    /// <summary>
    /// Converts a PascalCase string to UPPER_SNAKE_CASE for use as a database column name.
    /// </summary>
    /// <param name="pascalCase">The PascalCase property name to convert.</param>
    /// <returns>The UPPER_SNAKE_CASE column name, or the original string if null/empty.</returns>
    public static string ToUpperSnakeCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var sb = new StringBuilder(pascalCase.Length + 4);

        for (int i = 0; i < pascalCase.Length; i++)
        {
            char c = pascalCase[i];

            if (i > 0 && char.IsUpper(c))
            {
                bool prevIsLower = char.IsLower(pascalCase[i - 1]);
                bool nextIsLower = i + 1 < pascalCase.Length && char.IsLower(pascalCase[i + 1]);

                // Insert underscore when:
                // 1. Previous char is lowercase (standard boundary: "bookingN" → "booking_N")
                // 2. Previous char is uppercase AND next is lowercase (acronym boundary: "HTTPs" → "HTTP_s")
                if (prevIsLower || (nextIsLower && char.IsUpper(pascalCase[i - 1])))
                {
                    sb.Append('_');
                }
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }
}
