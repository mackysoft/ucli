using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.recovered</c> stream payload. </summary>
public sealed record CompileRecoveredEntry
{
    /// <summary> Initializes one compile recovery entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public CompileRecoveredEntry (
        Guid RunId,
        string SummaryJsonPath,
        string? DispatchFailureCode,
        int PollAttempts)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.SummaryJsonPath = SummaryJsonPath;
        this.DispatchFailureCode = DispatchFailureCode;
        this.PollAttempts = PollAttempts;
    }

    public Guid RunId { get; }

    public string SummaryJsonPath { get; }

    public string? DispatchFailureCode { get; }

    public int PollAttempts { get; }
}
