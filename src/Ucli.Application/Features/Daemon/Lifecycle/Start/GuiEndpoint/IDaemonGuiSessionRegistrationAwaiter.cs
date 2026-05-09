namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Waits until a GUI Editor process registers a matching daemon session. </summary>
internal interface IDaemonGuiSessionRegistrationAwaiter
{
    /// <summary> Waits for a GUI session belonging to the expected process identifier. </summary>
    ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        TimeSpan timeout,
        DateTimeOffset? expectedProcessStartedAtUtc = null,
        CancellationToken cancellationToken = default);
}
