namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Waits until a GUI Editor process registers a matching daemon session. </summary>
internal interface IDaemonGuiSessionRegistrationAwaiter
{
    /// <summary> Waits for a GUI session belonging to the expected process identifier. </summary>
    ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSession (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
