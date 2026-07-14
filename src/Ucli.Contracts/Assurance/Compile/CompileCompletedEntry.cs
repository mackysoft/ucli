using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.completed</c> stream payload. </summary>
public sealed record CompileCompletedEntry
{
    /// <summary> Initializes one compile completion entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public CompileCompletedEntry (
        Guid RunId,
        string Verdict,
        int ErrorCount,
        int WarningCount,
        string SummaryJsonPath,
        string DiagnosticsJsonPath)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.Verdict = Verdict;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.SummaryJsonPath = SummaryJsonPath;
        this.DiagnosticsJsonPath = DiagnosticsJsonPath;
    }

    public Guid RunId { get; }

    public string Verdict { get; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public string SummaryJsonPath { get; }

    public string DiagnosticsJsonPath { get; }
}
