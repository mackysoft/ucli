namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Represents normalized payload values for one daemon-list command execution. </summary>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon probing. </param>
/// <param name="ProjectRelativePath"> The current Unity project path relative to the current Git worktree root. </param>
/// <param name="IsComplete"> Whether all candidate worktrees were fully observed before the shared deadline expired. </param>
/// <param name="CompletionReason"> The reason when the result is partial; otherwise <see langword="null" />. </param>
/// <param name="RemainingWorktreeCount"> The number of worktrees left unobserved when the result is partial; otherwise <c>0</c>. </param>
/// <param name="Items"> The daemon registration observations returned from worktree enumeration. </param>
internal sealed record DaemonListExecutionOutput (
    int TimeoutMilliseconds,
    string ProjectRelativePath,
    bool IsComplete,
    string? CompletionReason,
    int RemainingWorktreeCount,
    IReadOnlyList<DaemonListItemOutput> Items);