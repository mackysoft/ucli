namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Represents one daemon registration observation returned by <c>daemon list</c>. </summary>
/// <param name="WorktreePath"> The Git worktree root path. </param>
/// <param name="BranchRef"> The branch ref when attached; otherwise <see langword="null" />. </param>
/// <param name="Head"> The HEAD commit hash. </param>
/// <param name="ProjectPath"> The Unity project root path under the worktree. </param>
/// <param name="ProjectFingerprint"> The deterministic project fingerprint resolved for the worktree-local storage root. </param>
/// <param name="State"> The daemon observation state. </param>
/// <param name="Reason"> The optional failure reason for non-running states. </param>
/// <param name="IssuedAtUtc"> The daemon session issuance timestamp when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="ProcessId"> The daemon process identifier when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="EndpointTransportKind"> The daemon endpoint transport kind when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="EndpointAddress"> The daemon endpoint address when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="Diagnosis"> The daemon diagnosis values when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonListItemOutput (
    string WorktreePath,
    string? BranchRef,
    string Head,
    string ProjectPath,
    string ProjectFingerprint,
    string State,
    string? Reason,
    DateTimeOffset? IssuedAtUtc,
    int? ProcessId,
    string? EndpointTransportKind,
    string? EndpointAddress,
    DaemonDiagnosisOutput? Diagnosis);