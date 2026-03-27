namespace Muonroi.Data.Dapper.Dapper.Handlers;

/// <summary>
/// A Dapper type handler for <see cref="string"/> that trims leading and trailing whitespace.
/// </summary>
public class MTrimStringHandler : SqlMapper.TypeHandler<string>
{
    /// <summary>
    /// Parses a database value into a trimmed string.
    /// </summary>
    /// <param name="value">The database value.</param>
    /// <returns>A trimmed string representation of the value.</returns>
    public override string? Parse(object value)
    {
        return value.ToString()?.Trim();
    }

    /// <summary>
    /// Sets a database parameter to a string value.
    /// </summary>
    /// <param name="parameter">The database parameter.</param>
    /// <param name="value">The string value.</param>
    /// <exception cref="NotImplementedException">Thrown as this method is not currently implemented.</exception>
    public override void SetValue(IDbDataParameter parameter, string? value)
    {
        throw new NotImplementedException();
    }
}
