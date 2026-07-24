using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents a <c>build.run</c> progress stream payload. </summary>
public sealed record BuildProgressEntry
{
    /// <summary> Initializes one build progress entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Verdict" /> has an undefined value. </exception>
    [JsonConstructor]
    public BuildProgressEntry (
        Guid RunId,
        Sha256Digest ProfileDigest,
        BuildRunProgressPhase Phase,
        BuildRunnerKind? RunnerKind,
        IpcBuildReportResult? RunnerStatus,
        AssuranceVerdict? Verdict,
        IReadOnlyList<BuildArtifactKind> ReportRefs,
        UcliCode? ErrorCode)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        if (ProfileDigest == null)
        {
            throw new ArgumentNullException(nameof(ProfileDigest));
        }

        if (!TextVocabulary.IsDefined(Phase))
        {
            throw new ArgumentOutOfRangeException(nameof(Phase), Phase, "Build progress phase must be specified.");
        }
        if (RunnerKind.HasValue && !TextVocabulary.IsDefined(RunnerKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(RunnerKind), RunnerKind, "Build runner kind must be specified when present.");
        }
        if (RunnerStatus.HasValue && !TextVocabulary.IsDefined(RunnerStatus.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(RunnerStatus), RunnerStatus, "Build runner status must be specified when present.");
        }
        if (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
        }

        this.RunId = RunId;
        this.ProfileDigest = ProfileDigest;
        this.Phase = Phase;
        this.RunnerKind = RunnerKind;
        this.RunnerStatus = RunnerStatus;
        this.Verdict = Verdict;
        this.ReportRefs = SnapshotReportRefs(ReportRefs);
        this.ErrorCode = ErrorCode;
    }

    public Guid RunId { get; }

    public Sha256Digest ProfileDigest { get; }

    public BuildRunProgressPhase Phase { get; }

    public BuildRunnerKind? RunnerKind { get; }

    public IpcBuildReportResult? RunnerStatus { get; }

    public AssuranceVerdict? Verdict { get; }

    public IReadOnlyList<BuildArtifactKind> ReportRefs { get; }

    public UcliCode? ErrorCode { get; }

    private static IReadOnlyList<BuildArtifactKind> SnapshotReportRefs (IReadOnlyList<BuildArtifactKind>? reportRefs)
    {
        if (reportRefs == null)
        {
            throw new ArgumentNullException(nameof(ReportRefs));
        }

        if (reportRefs.Count == 0)
        {
            return Array.Empty<BuildArtifactKind>();
        }

        var snapshot = new BuildArtifactKind[reportRefs.Count];
        var seen = new HashSet<BuildArtifactKind>();
        for (var index = 0; index < reportRefs.Count; index++)
        {
            var reportRef = reportRefs[index];
            if (!TextVocabulary.IsDefined(reportRef))
            {
                throw new ArgumentOutOfRangeException(nameof(ReportRefs), reportRef, "Build report references must be defined.");
            }

            if (!seen.Add(reportRef))
            {
                throw new ArgumentException($"Build report references contain duplicate value '{reportRef}'.", nameof(ReportRefs));
            }

            snapshot[index] = reportRef;
        }

        return Array.AsReadOnly(snapshot);
    }
}
