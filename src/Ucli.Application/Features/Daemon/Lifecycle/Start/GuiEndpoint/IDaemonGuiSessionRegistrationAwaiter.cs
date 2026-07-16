namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Waits until a GUI Editor process registers a matching daemon session. </summary>
internal interface IDaemonGuiSessionRegistrationAwaiter
{
    /// <summary> Waits for a GUI session whose project and process identity match the expected Editor process. </summary>
    /// <param name="unityProject"> The project identity that the session and ping response must report. </param>
    /// <param name="expectedProcessId"> The positive Editor process identifier. </param>
    /// <param name="deadline"> The deadline shared by registration reads and reachability probes. </param>
    /// <param name="expectedProcessStartedAtUtc"> The verified Editor process start time that the session must match. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="expectedProcessStartedAtUtc" /> is the default value or does not use the UTC offset. </exception>
    ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        ExecutionDeadline deadline,
        DateTimeOffset expectedProcessStartedAtUtc,
        CancellationToken cancellationToken = default);
}
