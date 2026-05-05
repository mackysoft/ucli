using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Sends one daemon ping request and returns the decoded ping payload. </summary>
internal interface IDaemonPingInfoClient
{
    /// <summary> Sends one ping request to daemon for the specified project context and returns payload values. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="sessionToken"> Optional pre-resolved daemon session token. When <see langword="null" />, implementation resolves token from session storage. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the decoded ping payload. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    ValueTask<IpcPingResponse> PingAndRead (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        CancellationToken cancellationToken = default);
}
