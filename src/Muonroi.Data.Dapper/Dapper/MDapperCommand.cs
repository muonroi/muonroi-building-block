namespace Muonroi.Data.Dapper.Dapper;

public class MDapperCommand
{
    public string CommandText { get; set; } = string.Empty;
    public object? Parameters { get; set; }
    public IDbTransaction? Transaction { get; set; }
    public CommandType? CommandType { get; set; }
    public CommandFlags CommandFlag { get; set; } = CommandFlags.Buffered;

    public CommandDefinition Build(CancellationToken cancellationToken)
    {
        return new CommandDefinition(
            CommandText,
            Parameters,
            Transaction,
            null,
            CommandType,
            CommandFlag,
            cancellationToken);
    }
}
