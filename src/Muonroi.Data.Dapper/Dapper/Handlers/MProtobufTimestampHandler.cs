namespace Muonroi.Data.Dapper.Dapper.Handlers;

/// <summary>
/// A Dapper type handler for <see cref="Google.Protobuf.WellKnownTypes.Timestamp"/>.
/// </summary>
public class MProtobufTimestampHandler : SqlMapper.TypeHandler<Google.Protobuf.WellKnownTypes.Timestamp>
{
    /// <summary>
    /// Parses a database value into a <see cref="Google.Protobuf.WellKnownTypes.Timestamp"/>.
    /// </summary>
    /// <param name="value">The database value.</param>
    /// <returns>A <see cref="Google.Protobuf.WellKnownTypes.Timestamp"/> representation of the value.</returns>
    public override Google.Protobuf.WellKnownTypes.Timestamp Parse(object value)
    {
        return Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc));
    }

    /// <summary>
    /// Sets a database parameter to a <see cref="Google.Protobuf.WellKnownTypes.Timestamp"/> value.
    /// </summary>
    /// <param name="parameter">The database parameter.</param>
    /// <param name="value">The <see cref="Google.Protobuf.WellKnownTypes.Timestamp"/> value.</param>
    public override void SetValue(IDbDataParameter parameter, Google.Protobuf.WellKnownTypes.Timestamp? value)
    {
        parameter.Value = value;
    }
}
