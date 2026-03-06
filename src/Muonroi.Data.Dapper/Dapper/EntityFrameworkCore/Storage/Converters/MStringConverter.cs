namespace Muonroi.Data.Dapper.Dapper.EntityFrameworkCore.Storage.Converters;

public class MStringConverter() : ValueConverter<string, string>(v => v,
    v => v.Trim());
