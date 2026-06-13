namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>build.run.completed</c> stream payload. </summary>
public sealed record BuildRunCompletedEntry (
    string RunId,
    string Verdict,
    string Result,
    string CompletionReason,
    int ErrorCount,
    int WarningCount,
    string BuildJsonPath,
    string BuildReportPath,
    string BuildLogPath,
    string OutputManifestPath);
