using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Sends daemon ping requests over IPC transport. </summary>
internal sealed class IpcDaemonPingClient : IDaemonPingClient, IDaemonPingInfoClient
{
    private const string ProbeClientVersion = "ucli-mode-probe";

    private readonly IUnityIpcClient unityIpcClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonPingClient" /> class. </summary>
    /// <param name="unityIpcClient"> The Unity IPC client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session token provider dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public IpcDaemonPingClient (
        IUnityIpcClient unityIpcClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
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
        EnsureSuccessfulPingResponse(response);
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
        EnsureSuccessfulPingResponse(response);

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse payload, out var readError))
        {
            throw new DaemonPingResponseException($"Daemon ping payload is invalid. {readError.Message}");
        }

        if (string.IsNullOrWhiteSpace(payload.ServerVersion)
            || string.IsNullOrWhiteSpace(payload.Runtime)
            || string.IsNullOrWhiteSpace(payload.UnityVersion)
            || string.IsNullOrWhiteSpace(payload.CompileState))
        {
            throw new DaemonPingResponseException("Daemon ping payload is invalid. One or more required fields are empty.");
        }

        return payload;
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

        return await unityIpcClient.SendAsync(
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

    /// <summary> Validates ping response status and error payload. </summary>
    /// <param name="response"> The response returned from daemon. </param>
    /// <exception cref="DaemonPingResponseException"> Thrown when daemon reports non-success status or error entries. </exception>
    private static void EnsureSuccessfulPingResponse (IpcResponse response)
    {
        if (!string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal))
        {
            if (response.Errors.Count > 0)
            {
                var firstError = response.Errors[0];
                throw new DaemonPingResponseException(
                    $"Daemon ping failed with error code '{firstError.Code}'.",
                    firstError.Code);
            }

            throw new DaemonPingResponseException($"Daemon ping failed with status '{response.Status}'.");
        }

        if (response.Errors.Count > 0)
        {
            var firstError = response.Errors[0];
            throw new DaemonPingResponseException(
                $"Daemon ping failed with error code '{firstError.Code}'.",
                firstError.Code);
        }
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