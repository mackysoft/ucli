using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents a <c>build.run</c> progress stream payload. </summary>
public sealed record BuildProgressEntry
{
    /// <summary> Initializes one build progress entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public BuildProgressEntry (
        Guid RunId,
        string ProfileDigest,
        string Phase,
        string? RunnerKind,
        string? RunnerStatus,
        string? Verdict,
        string[] ReportRefs,
        string? ErrorCode)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.ProfileDigest = ProfileDigest;
        this.Phase = Phase;
        this.RunnerKind = RunnerKind;
        this.RunnerStatus = RunnerStatus;
        this.Verdict = Verdict;
        this.ReportRefs = ReportRefs;
        this.ErrorCode = ErrorCode;
    }

    public Guid RunId { get; }

    public string ProfileDigest { get; }

    public string Phase { get; }

    public string? RunnerKind { get; }

    public string? RunnerStatus { get; }

    public string? Verdict { get; }

    public string[] ReportRefs { get; }

    public string? ErrorCode { get; }
}
