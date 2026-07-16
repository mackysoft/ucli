using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.completed</c> stream payload. </summary>
public sealed record CompileCompletedEntry
{
    /// <summary> Initializes one compile completion entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when a required path is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a diagnostic count is negative or <paramref name="Verdict" /> has an undefined value. </exception>
    [JsonConstructor]
    public CompileCompletedEntry (
        Guid RunId,
        AssuranceVerdict Verdict,
        int ErrorCount,
        int WarningCount,
        string SummaryJsonPath,
        string DiagnosticsJsonPath)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }
        if (!ContractLiteralCodec.IsDefined(Verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
        }
        if (ErrorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ErrorCount), ErrorCount, "Error count must not be negative.");
        }
        if (WarningCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(WarningCount), WarningCount, "Warning count must not be negative.");
        }

        this.RunId = RunId;
        this.Verdict = Verdict;
        this.ErrorCount = ErrorCount;
        this.WarningCount = WarningCount;
        this.SummaryJsonPath = SummaryJsonPath ?? throw new ArgumentNullException(nameof(SummaryJsonPath));
        this.DiagnosticsJsonPath = DiagnosticsJsonPath ?? throw new ArgumentNullException(nameof(DiagnosticsJsonPath));
    }

    public Guid RunId { get; }

    [JsonInclude]
    [JsonRequired]
    public AssuranceVerdict Verdict { get; private init; }

    public int ErrorCount { get; }

    public int WarningCount { get; }

    public string SummaryJsonPath { get; }

    public string DiagnosticsJsonPath { get; }
}
