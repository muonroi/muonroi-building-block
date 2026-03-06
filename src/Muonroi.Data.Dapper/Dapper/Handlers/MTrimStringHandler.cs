namespace Muonroi.Data.Dapper.Dapper.Handlers;

public class MTrimStringHandler : SqlMapper.TypeHandler<string>
{
    public override string? Parse(object value)
    {
        return value.ToString()?.Trim();
    }

    public override void SetValue(IDbDataParameter parameter, string? value)
    {
        throw new NotImplementedException();
    }
}
