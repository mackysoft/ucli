namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.completed</c> stream payload. </summary>
public sealed record CompileCompletedEntry (
    Guid RunId,
    string Verdict,
    int ErrorCount,
    int WarningCount,
    string SummaryJsonPath,
    string DiagnosticsJsonPath);
