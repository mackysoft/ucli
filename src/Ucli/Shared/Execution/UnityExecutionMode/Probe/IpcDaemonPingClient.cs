using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Sends daemon ping requests over IPC transport. </summary>
internal sealed class IpcDaemonPingClient : IDaemonPingClient, IDaemonPingInfoClient
{
    private const string ProbeClientVersion = "ucli-mode-probe";

    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonPingClient" /> class. </summary>
    /// <param name="transportClient"> The shared Unity IPC transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session token provider dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public IpcDaemonPingClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
    }

    /// <summary> Sends one ping request and waits for daemon response. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="sessionToken"> Optional pre-resolved daemon session token. When <see langword="null" />, this method resolves token from session storage. </param>
    /// <param name="validateProjectFingerprint"> Whether to reject a decoded ping payload whose project fingerprint differs from <paramref name="unityProject" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when daemon responds. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    public async ValueTask PingAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendPingRequestAsync(unityProject, timeout, sessionToken, cancellationToken).ConfigureAwait(false);
        if (!DaemonPingResponseCodec.TryDecodePayloadForProject(
                response,
                unityProject.ProjectFingerprint,
                "Daemon ping",
                out _,
                out var error))
        {
            throw error!;
        }
    }

    /// <summary> Sends one ping request and returns decoded ping payload values. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="sessionToken"> Optional pre-resolved daemon session token. When <see langword="null" />, this method resolves token from session storage. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the decoded ping payload. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    /// <exception cref="DaemonPingResponseException"> Thrown when daemon reports contract failures or payload deserialization fails. </exception>
    public async ValueTask<IpcPingResponse> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        bool validateProjectFingerprint = true,
        CancellationToken cancellationToken = default)
    {
        var response = await SendPingRequestAsync(unityProject, timeout, sessionToken, cancellationToken).ConfigureAwait(false);
        IpcPingResponse? payload;
        DaemonPingResponseException? error;
        var isDecoded = validateProjectFingerprint
            ? DaemonPingResponseCodec.TryDecodePayloadForProject(
                response,
                unityProject.ProjectFingerprint,
                "Daemon ping",
                out payload,
                out error)
            : DaemonPingResponseCodec.TryDecodePayload(response, out payload, out error);
        if (!isDecoded)
        {
            throw error!;
        }

        return payload!;
    }

    /// <summary> Sends one ping request and returns the raw IPC response envelope. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="sessionToken"> Optional pre-resolved daemon session token. When <see langword="null" />, this method resolves token from session storage. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the raw ping response. </returns>
    private async ValueTask<IpcResponse> SendPingRequestAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveSessionToken = await ResolveSessionTokenAsync(unityProject, sessionToken, cancellationToken).ConfigureAwait(false);

        return await transportClient.SendAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                CreatePingRequest(effectiveSessionToken),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Resolves the effective session token from explicit input or session storage. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="sessionToken"> The optional explicit session token input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the effective session token. </returns>
    private async ValueTask<string> ResolveSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (sessionToken != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
            return sessionToken;
        }

        var sessionTokenResult = await daemonSessionTokenProvider.ResolveAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (!sessionTokenResult.IsSuccess)
        {
            if (sessionTokenResult.IsSessionNotAvailable)
            {
                throw new DaemonPingResponseException(
                    "Daemon session token is required.",
                    IpcSessionErrorCodes.SessionTokenRequired);
            }

            throw new DaemonPingResponseException(
                $"Daemon session token could not be resolved. {sessionTokenResult.Error!.Message}");
        }

        return sessionTokenResult.Token!;
    }

    /// <summary> Creates one IPC ping request used for daemon reachability probing. </summary>
    /// <returns> The ping request envelope. </returns>
    private static IpcRequest CreatePingRequest (string sessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        var payload = IpcPayloadCodec.SerializeToElement(new IpcPingRequest(ProbeClientVersion));
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"mode-probe-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: IpcMethodNames.Ping,
            Payload: payload);
    }

}
