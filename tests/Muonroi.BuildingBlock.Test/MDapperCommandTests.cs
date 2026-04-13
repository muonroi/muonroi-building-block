namespace Muonroi.BuildingBlock.Test;

public class MDapperCommandTests
{
    private class FakeDbTransaction : IDbTransaction
    {
        private bool _disposed = false;

        public IDbConnection Connection => throw new NotImplementedException();
        public IsolationLevel IsolationLevel => IsolationLevel.Unspecified;

        public void Commit()
        {
        }

        public void Rollback()
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed) _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    [Fact]
    public void CommandText_GetSet_Works()
    {
        MDapperCommand cmd = new();
        Assert.Equal(string.Empty, cmd.CommandText);
        cmd.CommandText = "SELECT 1";
        Assert.Equal("SELECT 1", cmd.CommandText);
    }

    [Fact]
    public void Parameters_GetSet_Works()
    {
        MDapperCommand cmd = new();
        Assert.Null(cmd.Parameters);
        object param = new { Id = 1 };
        cmd.Parameters = param;
        Assert.Same(param, cmd.Parameters);
    }

    [Fact]
    public void Transaction_GetSet_Works()
    {
        MDapperCommand cmd = new();
        Assert.Null(cmd.Transaction);
        IDbTransaction trans = new FakeDbTransaction();
        cmd.Transaction = trans;
        Assert.Same(trans, cmd.Transaction);
    }

    [Fact]
    public void CommandType_GetSet_Works()
    {
        MDapperCommand cmd = new();
        Assert.Null(cmd.CommandType);
        cmd.CommandType = CommandType.Text;
        Assert.Equal(CommandType.Text, cmd.CommandType);
    }

    [Fact]
    public void CommandFlag_GetSet_Works()
    {
        MDapperCommand cmd = new();
        Assert.Equal(CommandFlags.Buffered, cmd.CommandFlag);
        cmd.CommandFlag = CommandFlags.Pipelined;
        Assert.Equal(CommandFlags.Pipelined, cmd.CommandFlag);
    }

    [Fact]
    public void Build_Returns_CommandDefinition_With_Values()
    {
        MDapperCommand cmd = new()
        {
            CommandText = "SELECT 1",
            Parameters = new { Id = 1 },
            Transaction = new FakeDbTransaction(),
            CommandType = CommandType.Text,
            CommandFlag = CommandFlags.Pipelined
        };

        CommandDefinition def = cmd.Build(CancellationToken.None);

        Assert.Equal("SELECT 1", def.CommandText);
        Assert.Equal(cmd.Parameters, def.Parameters);
        Assert.Equal(cmd.Transaction, def.Transaction);
        Assert.Equal(cmd.CommandType, def.CommandType);
        Assert.Equal(cmd.CommandFlag, def.Flags);
    }

    [Fact]
    public void Build_With_Null_Parameters_Succeeds()
    {
        MDapperCommand cmd = new()
        {
            CommandText = "SELECT 1"
        };
        CommandDefinition def = cmd.Build(CancellationToken.None);
        Assert.Equal("SELECT 1", def.CommandText);
        Assert.Null(def.Parameters);
    }
}
