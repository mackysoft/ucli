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

    private readonly IIpcTransportClient transportClient;

    private readonly IDaemonSessionConnectionProvider daemonSessionConnectionProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonPingClient" /> class. </summary>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="daemonSessionConnectionProvider"> The daemon session connection provider dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public IpcDaemonPingClient (
        IIpcTransportClient transportClient,
        IDaemonSessionConnectionProvider daemonSessionConnectionProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionConnectionProvider = daemonSessionConnectionProvider ?? throw new ArgumentNullException(nameof(daemonSessionConnectionProvider));
    }

    /// <summary> Sends one ping request and waits for daemon response. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="sessionToken"> Optional session token override. The daemon endpoint is resolved from session storage. </param>
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
    /// <param name="sessionToken"> Optional session token override. The daemon endpoint is resolved from session storage. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the decoded ping payload. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    /// <exception cref="DaemonPingResponseException"> Thrown when daemon reports contract failures or payload deserialization fails. </exception>
    public async ValueTask<IpcUnityEditorObservation> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        bool validateProjectFingerprint = true,
        CancellationToken cancellationToken = default)
    {
        var response = await SendPingRequestAsync(unityProject, timeout, sessionToken, cancellationToken).ConfigureAwait(false);
        IpcUnityEditorObservation? payload;
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
    /// <param name="sessionToken"> Optional session token override. The daemon endpoint is resolved from session storage. </param>
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

        var sessionConnection = await ResolveSessionConnectionAsync(unityProject, cancellationToken).ConfigureAwait(false);
        var effectiveSessionToken = ResolveSessionToken(sessionToken, sessionConnection.SessionToken);

        return await transportClient.SendAsync(
                sessionConnection.Endpoint,
                CreatePingRequest(effectiveSessionToken),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Resolves daemon session connection values from session storage. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the daemon session connection values. </returns>
    private async ValueTask<DaemonSessionConnection> ResolveSessionConnectionAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var sessionConnectionResult = await daemonSessionConnectionProvider.ResolveAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (!sessionConnectionResult.IsSuccess)
        {
            if (sessionConnectionResult.IsSessionNotAvailable)
            {
                throw new DaemonPingResponseException(
                    "Daemon session is required.",
                    IpcSessionErrorCodes.SessionTokenRequired);
            }

            throw new DaemonPingResponseException(
                $"Daemon session connection could not be resolved. {sessionConnectionResult.Error!.Message}");
        }

        return sessionConnectionResult.Connection!;
    }

    /// <summary> Resolves the effective session token from explicit input or persisted session connection values. </summary>
    /// <param name="sessionToken"> The optional explicit session token input. </param>
    /// <param name="persistedSessionToken"> The session token resolved from persisted session metadata. </param>
    /// <returns> The effective session token. </returns>
    private static string ResolveSessionToken (
        string? sessionToken,
        string persistedSessionToken)
    {
        if (sessionToken != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
            return sessionToken;
        }

        return persistedSessionToken;
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
            Payload: payload,
            responseMode: IpcResponseMode.Single);
    }

}
