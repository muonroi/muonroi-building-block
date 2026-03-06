namespace Muonroi.BuildingBlock.Test;

public class MSqlMapperTypeExtensionsTests
{
    [Fact]
    public void RegisterDapperHandlers_Adds_Handlers()
    {
        SqlMapper.ResetTypeHandlers();
        Assert.False(SqlMapper.HasTypeHandler(typeof(Timestamp)));
        MSqlMapperTypeExtensions.RegisterDapperHandlers();
        Assert.True(SqlMapper.HasTypeHandler(typeof(Timestamp)));
        Assert.True(SqlMapper.HasTypeHandler(typeof(string)));
    }

    [Fact]
    public void RegisterDapperHandlers_Multiple_Calls_Succeeds()
    {
        SqlMapper.ResetTypeHandlers();
        MSqlMapperTypeExtensions.RegisterDapperHandlers();
        MSqlMapperTypeExtensions.RegisterDapperHandlers();
        Assert.True(SqlMapper.HasTypeHandler(typeof(Timestamp)));
    }
}
