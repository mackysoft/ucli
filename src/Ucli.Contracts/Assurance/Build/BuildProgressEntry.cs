namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents a <c>build.run</c> progress stream payload. </summary>
public sealed record BuildProgressEntry (
    Guid RunId,
    string ProfileDigest,
    string Phase,
    string? RunnerKind,
    string? RunnerStatus,
    string? Verdict,
    string[] ReportRefs,
    string? ErrorCode);
