using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Sends one daemon ping request and returns the decoded ping payload. </summary>
internal interface IDaemonPingInfoClient
{
    /// <summary> Sends one ping request to daemon for the specified project context and returns payload values. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="validateProjectFingerprint"> Whether to reject a decoded ping payload whose project fingerprint differs from <paramref name="unityProject" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the decoded ping payload. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    ValueTask<IpcUnityEditorObservation> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Sends one ping request to the endpoint and token captured in one daemon session snapshot. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The daemon session snapshot whose endpoint and token must be used together. </param>
    /// <param name="requestId"> The non-empty identifier of the logical ping request, shared by any replacement-session delivery. </param>
    /// <param name="deadline"> The deadline shared with the logical ping request. </param>
    /// <param name="validateProjectFingerprint"> Whether to reject a decoded ping payload whose project fingerprint differs from <paramref name="unityProject" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the decoded ping payload. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" />, <paramref name="session" />, or <paramref name="deadline" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    ValueTask<IpcUnityEditorObservation> PingSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        Guid requestId,
        ExecutionDeadline deadline,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken);
}
