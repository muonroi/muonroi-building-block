namespace Muonroi.Data.Dapper.Dapper.Handlers;

public class MProtobufTimestampHandler : SqlMapper.TypeHandler<Google.Protobuf.WellKnownTypes.Timestamp>
{
    public override Google.Protobuf.WellKnownTypes.Timestamp Parse(object value)
    {
        return Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc));
    }

    public override void SetValue(IDbDataParameter parameter, Google.Protobuf.WellKnownTypes.Timestamp? value)
    {
        parameter.Value = value;
    }
}
