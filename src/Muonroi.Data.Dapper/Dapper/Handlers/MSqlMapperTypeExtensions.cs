namespace Muonroi.Data.Dapper.Dapper.Handlers;

/// <summary>
/// Provides extension methods for registering custom Dapper type handlers.
/// </summary>
public static class MSqlMapperTypeExtensions
{
    /// <summary>
    /// Registers custom Dapper type handlers for Protobuf timestamps and trimmed strings.
    /// </summary>
    public static void RegisterDapperHandlers()
    {
        SqlMapper.ResetTypeHandlers();
        SqlMapper.AddTypeHandler(new MProtobufTimestampHandler());
        SqlMapper.AddTypeHandler(new MTrimStringHandler());
    }
}
