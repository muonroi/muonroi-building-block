namespace Muonroi.Data.Dapper.Tests;

public class MDapperCommandTests
{
    private class FakeDbTransaction : IDbTransaction
    {
        public IDbConnection Connection => throw new NotImplementedException();

        public IsolationLevel IsolationLevel => IsolationLevel.Unspecified;

        public void Commit()
        {
        }

        public void Rollback()
        {
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void CommandText_GetSet_Works()
    {
        MDapperCommand command = new();

        Assert.Equal(string.Empty, command.CommandText);

        command.CommandText = "SELECT 1";

        Assert.Equal("SELECT 1", command.CommandText);
    }

    [Fact]
    public void Parameters_GetSet_Works()
    {
        MDapperCommand command = new();
        object parameters = new { Id = 1 };

        Assert.Null(command.Parameters);

        command.Parameters = parameters;

        Assert.Same(parameters, command.Parameters);
    }

    [Fact]
    public void Transaction_GetSet_Works()
    {
        MDapperCommand command = new();
        IDbTransaction transaction = new FakeDbTransaction();

        Assert.Null(command.Transaction);

        command.Transaction = transaction;

        Assert.Same(transaction, command.Transaction);
    }

    [Fact]
    public void CommandType_GetSet_Works()
    {
        MDapperCommand command = new();

        Assert.Null(command.CommandType);

        command.CommandType = CommandType.Text;

        Assert.Equal(CommandType.Text, command.CommandType);
    }

    [Fact]
    public void CommandFlag_GetSet_Works()
    {
        MDapperCommand command = new();

        Assert.Equal(CommandFlags.Buffered, command.CommandFlag);

        command.CommandFlag = CommandFlags.Pipelined;

        Assert.Equal(CommandFlags.Pipelined, command.CommandFlag);
    }

    [Fact]
    public void Build_Returns_CommandDefinition_With_Values()
    {
        MDapperCommand command = new()
        {
            CommandText = "SELECT 1",
            Parameters = new { Id = 1 },
            Transaction = new FakeDbTransaction(),
            CommandType = CommandType.Text,
            CommandFlag = CommandFlags.Pipelined
        };

        CommandDefinition definition = command.Build(CancellationToken.None);

        Assert.Equal("SELECT 1", definition.CommandText);
        Assert.Equal(command.Parameters, definition.Parameters);
        Assert.Equal(command.Transaction, definition.Transaction);
        Assert.Equal(command.CommandType, definition.CommandType);
        Assert.Equal(command.CommandFlag, definition.Flags);
    }

    [Fact]
    public void Build_With_Null_Parameters_Succeeds()
    {
        MDapperCommand command = new()
        {
            CommandText = "SELECT 1"
        };

        CommandDefinition definition = command.Build(CancellationToken.None);

        Assert.Equal("SELECT 1", definition.CommandText);
        Assert.Null(definition.Parameters);
    }
}
