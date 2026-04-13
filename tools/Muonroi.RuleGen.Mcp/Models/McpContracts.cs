using System.Text.Json.Serialization;

namespace Muonroi.RuleGen.Mcp.Models;

internal sealed record RuleGenExtractResult(
    int ExtractedCount,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

internal sealed record RuleGenVerifyResult(
    bool Passed,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> ExtraFiles);

internal sealed record RuleGenRegisterResult(
    string RegistrationFile,
    int DispatcherCount,
    int RuleCount,
    IReadOnlyList<string> DispatcherFiles);

internal sealed record RuleGenGenerateTestsResult(
    int GeneratedCount,
    IReadOnlyList<string> Files);

internal sealed record RuleGenMergeResult(
    int MergedRuleCount,
    string OutputPath,
    IReadOnlyList<string> OutputFiles);

internal sealed record RuleGenSplitResult(
    int SplitCount,
    IReadOnlyList<string> Files,
    string? ExportJsonPath);

internal sealed record RuleGenWatchResult(
    bool Started,
    bool InitialExtractSucceeded,
    int EventCount,
    DateTime? LastRunAtUtc,
    IReadOnlyList<string> Files);

internal sealed record DecisionTableImportResult(
    string TableName,
    int RowCount,
    int ColumnCount,
    string OutputPath);

internal sealed record DecisionTableValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

internal sealed record DecisionTableExportResult(
    string OutputPath,
    int RuleCount);

internal sealed record ComplianceViolation(
    string Code,
    string Severity,
    string? File,
    int? Line,
    int? Column,
    string Message,
    string? SuggestedFix,
    string? RawCode,
    string? ExemptionComment);

internal sealed record ComplianceCheckResult(
    IReadOnlyList<ComplianceViolation> Violations,
    int ErrorCount,
    int WarningCount,
    int PassedFiles,
    string AnalysisMode,
    IReadOnlyList<string> AnalyzedFiles,
    IReadOnlyList<string> Notes);

internal sealed record OssBoundaryViolation(
    string OssPackage,
    string IllegalRef,
    string File);

internal sealed record OssBoundaryCheckResult(
    bool Passed,
    IReadOnlyList<OssBoundaryViolation> Violations,
    IReadOnlyList<string> Notes);

internal sealed record EcosystemRuleDescriptor(
    string Code,
    string Severity,
    string Description,
    IReadOnlyList<string> Forbidden,
    string Required,
    IReadOnlyList<string> Methods,
    string Injection,
    IReadOnlyList<string> ExemptFiles,
    string ExemptComment,
    string SuggestedFix);

internal sealed record WrapperSuggestionResult(
    string Original,
    string Corrected,
    string RequiredDependency,
    string InjectionPattern,
    bool ExemptionAvailable,
    string MbbRule,
    string? ExemptionComment);

internal sealed record PolicySignResult(
    string OutputPath,
    string PolicyId,
    string LicenseId,
    string SignatureAlgorithm,
    DateTimeOffset? ExpiresAtUtc);

internal sealed record PolicyVerifyResult(
    bool IsValid,
    bool SignatureValid,
    bool IsExpired,
    string? PolicyId,
    string? LicenseId,
    IReadOnlyList<string> Errors);

internal sealed record ScaffoldResult(
    string Filename,
    string Code,
    string? SecondaryFilename = null,
    string? SecondaryCode = null);
