namespace Muonroi.RuleEngine.DecisionTable.Dmn;

/// <summary>
/// Result of a DMN XML import operation.
/// </summary>
/// <param name="IsSuccess">Whether the import succeeded.</param>
/// <param name="Table">The imported decision table, or <c>null</c> if import failed.</param>
/// <param name="Warnings">Non-fatal warnings (e.g., multiple decisions found — only first imported).</param>
/// <param name="Errors">Fatal error messages that caused import to fail.</param>
public sealed record DmnImportResult(
    bool IsSuccess,
    DecisionTableModel? Table,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors)
{
    /// <summary>Creates a successful import result.</summary>
    public static DmnImportResult Success(DecisionTableModel table, IReadOnlyList<string>? warnings = null) =>
        new(true, table, warnings ?? [], []);

    /// <summary>Creates a failure import result with one or more error messages.</summary>
    public static DmnImportResult Failure(params string[] errors) =>
        new(false, null, [], errors);
}
