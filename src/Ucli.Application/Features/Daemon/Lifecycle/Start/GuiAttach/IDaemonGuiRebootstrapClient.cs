namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Requests GUI daemon endpoint rebootstrap through the GUI supervisor endpoint. </summary>
internal interface IDaemonGuiRebootstrapClient
{
    /// <summary> Requests daemon endpoint rebootstrap for the detected GUI Editor process. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="expectedProcessId"> The GUI Editor process identifier detected from <c>Library/EditorInstance.json</c>. </param>
    /// <param name="expectedProcessStartedAtUtc"> The validated GUI process start timestamp when available. </param>
    /// <param name="timeout"> The timeout for manifest read and IPC request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The rebootstrap request result. </returns>
    ValueTask<DaemonGuiRebootstrapRequestResult> RequestRebootstrapAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
