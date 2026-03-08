namespace Muonroi.Data.Dapper.Dapper;

/// <summary>
/// Represents a Dapper command with text, parameters, and other configuration options.
/// </summary>
public class MDapperCommand
{
    /// <summary>
    /// Gets or sets the command text to execute.
    /// </summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters for the command.
    /// </summary>
    public object? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the transaction to use for the command.
    /// </summary>
    public IDbTransaction? Transaction { get; set; }

    /// <summary>
    /// Gets or sets the type of command to execute.
    /// </summary>
    public CommandType? CommandType { get; set; }

    /// <summary>
    /// Gets or sets the flags for the command.
    /// </summary>
    public CommandFlags CommandFlag { get; set; } = CommandFlags.Buffered;

    /// <summary>
    /// Builds a <see cref="CommandDefinition"/> from the command configuration.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A new instance of <see cref="CommandDefinition"/>.</returns>
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
