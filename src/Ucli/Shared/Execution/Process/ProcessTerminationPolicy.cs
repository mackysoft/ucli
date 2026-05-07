namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Describes process termination behavior for timeout and cleanup paths. </summary>
internal sealed record ProcessTerminationPolicy
{
    /// <summary> Gets the default force-kill policy used by generic process execution. </summary>
    public static ProcessTerminationPolicy ForceKill { get; } = new(
        ProcessTerminationMode.ForceKill,
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5));

    /// <summary> Initializes a new instance of the <see cref="ProcessTerminationPolicy" /> class. </summary>
    /// <param name="mode"> The termination mode. </param>
    /// <param name="graceTimeout"> The graceful-exit wait budget. </param>
    /// <param name="forceKillWaitTimeout"> The force-kill wait budget. </param>
    public ProcessTerminationPolicy (
        ProcessTerminationMode mode,
        TimeSpan graceTimeout,
        TimeSpan forceKillWaitTimeout)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown process termination mode.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(graceTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(forceKillWaitTimeout, TimeSpan.Zero);

        Mode = mode;
        GraceTimeout = graceTimeout;
        ForceKillWaitTimeout = forceKillWaitTimeout;
    }

    /// <summary> Gets the termination mode. </summary>
    public ProcessTerminationMode Mode { get; }

    /// <summary> Gets the graceful-exit wait budget. </summary>
    public TimeSpan GraceTimeout { get; }

    /// <summary> Gets the force-kill wait budget. </summary>
    public TimeSpan ForceKillWaitTimeout { get; }
}
