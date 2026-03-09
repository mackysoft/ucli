namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Represents normalized payload values for one daemon-list command execution. </summary>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon probing. </param>
/// <param name="ProjectRelativePath"> The current Unity project path relative to the current Git worktree root. </param>
/// <param name="Items"> The daemon registration observations returned from worktree enumeration. </param>
internal sealed record DaemonListExecutionOutput (
    int TimeoutMilliseconds,
    string ProjectRelativePath,
    IReadOnlyList<DaemonListItemOutput> Items);