using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon lifecycle facade by delegating start, stop, status, and log workflows. </summary>
internal sealed class DaemonManagementService : IDaemonManagementService
{
    private readonly IDaemonStartOperation startOperation;

    private readonly IDaemonStopOperation stopOperation;

    private readonly IDaemonStatusOperation statusOperation;

    private readonly IDaemonLogReader daemonLogReader;

    /// <summary> Initializes a new instance of the <see cref="DaemonManagementService" /> class. </summary>
    /// <param name="startOperation"> The daemon start operation dependency. </param>
    /// <param name="stopOperation"> The daemon stop operation dependency. </param>
    /// <param name="statusOperation"> The daemon status operation dependency. </param>
    /// <param name="daemonLogReader"> The daemon log reader dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonManagementService (
        IDaemonStartOperation startOperation,
        IDaemonStopOperation stopOperation,
        IDaemonStatusOperation statusOperation,
        IDaemonLogReader daemonLogReader)
    {
        this.startOperation = startOperation ?? throw new ArgumentNullException(nameof(startOperation));
        this.stopOperation = stopOperation ?? throw new ArgumentNullException(nameof(stopOperation));
        this.statusOperation = statusOperation ?? throw new ArgumentNullException(nameof(statusOperation));
        this.daemonLogReader = daemonLogReader ?? throw new ArgumentNullException(nameof(daemonLogReader));
    }

    /// <summary> Starts daemon process lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout used for daemon startup probing and IPC checks. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public ValueTask<DaemonStartResult> Start (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return startOperation.Start(unityProject, timeout, cancellationToken);
    }

    /// <summary> Stops daemon process lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout used for daemon shutdown IPC and process termination checks. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon stop result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public ValueTask<DaemonStopResult> Stop (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return stopOperation.Stop(unityProject, timeout, cancellationToken);
    }

    /// <summary> Gets daemon status for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout used for daemon reachability probe. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon status result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public ValueTask<DaemonStatusResult> GetStatus (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return statusOperation.GetStatus(unityProject, timeout, cancellationToken);
    }

    /// <summary> Reads daemon log tail for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="maxBytes"> The maximum number of bytes read from daemon log tail. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon log read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public ValueTask<DaemonLogReadResult> ReadLogs (
        ResolvedUnityProjectContext unityProject,
        int maxBytes = DaemonLogReader.DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        return daemonLogReader.ReadTail(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            maxBytes,
            cancellationToken);
    }
}