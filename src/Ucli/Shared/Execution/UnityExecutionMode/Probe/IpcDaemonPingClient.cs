using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

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
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when daemon responds. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    public async ValueTask Ping (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendPingRequest(unityProject, timeout, sessionToken, cancellationToken).ConfigureAwait(false);
        if (!DaemonPingResponseCodec.TryValidateSuccessResponse(response, out var error))
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
    public async ValueTask<IpcPingResponse> PingAndRead (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendPingRequest(unityProject, timeout, sessionToken, cancellationToken).ConfigureAwait(false);
        if (!DaemonPingResponseCodec.TryDecodePayload(response, out var payload, out var error))
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
    private async ValueTask<IpcResponse> SendPingRequest (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveSessionToken = await ResolveSessionToken(unityProject, sessionToken, cancellationToken).ConfigureAwait(false);

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
    private async ValueTask<string> ResolveSessionToken (
        ResolvedUnityProjectContext unityProject,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (sessionToken != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
            return sessionToken;
        }

        var sessionTokenResult = await daemonSessionTokenProvider.Resolve(unityProject, cancellationToken).ConfigureAwait(false);
        if (!sessionTokenResult.IsSuccess)
        {
            if (sessionTokenResult.IsSessionNotAvailable)
            {
                throw new DaemonPingResponseException(
                    "Daemon session token is required.",
                    IpcErrorCodes.SessionTokenRequired);
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