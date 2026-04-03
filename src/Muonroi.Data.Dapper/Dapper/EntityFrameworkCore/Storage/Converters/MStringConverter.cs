namespace Muonroi.Data.Dapper.Dapper.EntityFrameworkCore.Storage.Converters;

/// <summary>
/// An Entity Framework Core value converter that trims leading and trailing whitespace from strings.
/// </summary>
public class MStringConverter() : ValueConverter<string, string>(v => v,
    v => v.Trim());
