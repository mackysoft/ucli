using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Sends daemon ping requests over IPC transport. </summary>
internal sealed class IpcDaemonPingClient : IDaemonPingClient
{
    private const string ProbeSessionToken = "mode-probe";

    private const string ProbeClientVersion = "ucli-mode-probe";

    private readonly IUnityIpcClient unityIpcClient;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonPingClient" /> class. </summary>
    /// <param name="unityIpcClient"> The Unity IPC client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityIpcClient" /> is <see langword="null" />. </exception>
    public IpcDaemonPingClient (IUnityIpcClient unityIpcClient)
    {
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
    }

    /// <summary> Sends one ping request and waits for daemon response. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when daemon responds. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask Ping (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        await unityIpcClient.SendAsync(
                unityProject.UnityProjectRoot,
                unityProject.ProjectFingerprint,
                CreatePingRequest(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Creates one IPC ping request used for daemon reachability probing. </summary>
    /// <returns> The ping request envelope. </returns>
    private static IpcRequest CreatePingRequest ()
    {
        var payload = JsonSerializer.SerializeToElement(
            new IpcPingRequest(ProbeClientVersion),
            IpcJsonSerializerOptions.Default);
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"mode-probe-{Guid.NewGuid():N}",
            SessionToken: ProbeSessionToken,
            Method: IpcMethodNames.Ping,
            Payload: payload);
    }
}
