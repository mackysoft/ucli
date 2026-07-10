using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Sends daemon ping requests over IPC transport. </summary>
internal sealed class IpcDaemonPingClient : IDaemonPingClient, IDaemonPingInfoClient
{
    private const string ProbeClientVersion = "ucli-mode-probe";

    private readonly IIpcTransportClient transportClient;

    private readonly IDaemonSessionConnectionProvider daemonSessionConnectionProvider;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonPingClient" /> class. </summary>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="daemonSessionConnectionProvider"> The daemon session connection provider dependency. </param>
    /// <param name="timeProvider"> The time provider used for retry deadline accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public IpcDaemonPingClient (
        IIpcTransportClient transportClient,
        IDaemonSessionConnectionProvider daemonSessionConnectionProvider,
        TimeProvider timeProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionConnectionProvider = daemonSessionConnectionProvider ?? throw new ArgumentNullException(nameof(daemonSessionConnectionProvider));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask PingAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(unityProject, timeout, cancellationToken);
        _ = await PingCurrentSessionAndReadAsync(
                unityProject,
                timeout,
                validateProjectFingerprint: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PingSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ValidateRequest(unityProject, timeout, cancellationToken);
        var sessionConnection = CreateSessionConnection(session);
        var response = await SendPingRequestAsync(sessionConnection, timeout, cancellationToken).ConfigureAwait(false);
        _ = DecodeResponse(unityProject, response, validateProjectFingerprint: true);
    }

    /// <inheritdoc />
    public async ValueTask PingCanonicalEndpointWithTokenAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ValidateRequest(unityProject, timeout, cancellationToken);
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var explicitTokenConnection = new DaemonSessionConnection(sessionToken, endpoint);
        var response = await SendPingRequestAsync(explicitTokenConnection, timeout, cancellationToken).ConfigureAwait(false);
        _ = DecodeResponse(unityProject, response, validateProjectFingerprint: true);
    }

    /// <inheritdoc />
    public async ValueTask<IpcPingResponse> PingAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(unityProject, timeout, cancellationToken);
        return await PingCurrentSessionAndReadAsync(
                unityProject,
                timeout,
                validateProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IpcPingResponse> PingSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        ValidateRequest(unityProject, timeout, cancellationToken);
        var sessionConnection = CreateSessionConnection(session);
        var response = await SendPingRequestAsync(sessionConnection, timeout, cancellationToken).ConfigureAwait(false);
        return DecodeResponse(unityProject, response, validateProjectFingerprint);
    }

    private async ValueTask<IpcPingResponse> PingCurrentSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var sessionConnection = await ResolveSessionConnectionWithinDeadlineAsync(
                unityProject,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            return await SendPingAndDecodeWithinDeadlineAsync(
                    unityProject,
                    sessionConnection,
                    deadline,
                    validateProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DaemonPingResponseException exception) when (
            exception.ErrorCode == IpcSessionErrorCodes.SessionTokenInvalid)
        {
            var refreshedSessionConnection = await ResolveSessionConnectionWithinDeadlineAsync(
                    unityProject,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (Equals(refreshedSessionConnection, sessionConnection))
            {
                throw;
            }

            return await SendPingAndDecodeWithinDeadlineAsync(
                    unityProject,
                    refreshedSessionConnection,
                    deadline,
                    validateProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<IpcPingResponse> SendPingAndDecodeWithinDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionConnection sessionConnection,
        ExecutionDeadline deadline,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            throw new TimeoutException("Timed out while resolving the daemon session for ping.");
        }

        var response = await SendPingRequestAsync(
                sessionConnection,
                remainingTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            throw new TimeoutException("Timed out while probing the current daemon session.");
        }

        return DecodeResponse(unityProject, response, validateProjectFingerprint);
    }

    /// <summary> Sends one ping request and returns the raw IPC response envelope. </summary>
    /// <param name="sessionConnection"> The endpoint and token captured from the same session generation. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the raw ping response. </returns>
    private async ValueTask<IpcResponse> SendPingRequestAsync (
        DaemonSessionConnection sessionConnection,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionConnection);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        return await transportClient.SendAsync(
                sessionConnection.Endpoint,
                CreatePingRequest(sessionConnection.SessionToken),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonSessionConnection> ResolveSessionConnectionWithinDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var resolutionOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon session resolution for ping could begin.",
                "Timed out while resolving daemon session for ping.",
                token => ResolveSessionConnectionAsync(unityProject, token))
            .ConfigureAwait(false);
        if (!resolutionOperation.IsSuccess)
        {
            throw new TimeoutException(resolutionOperation.Error!.Message);
        }

        return resolutionOperation.Value!;
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

    private static DaemonSessionConnection CreateSessionConnection (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!DaemonSessionConnectionFactory.TryCreate(session, out var sessionConnection, out var error))
        {
            throw new DaemonPingResponseException(
                $"Daemon session connection could not be created. {error!.Message}");
        }

        return sessionConnection;
    }

    private static IpcPingResponse DecodeResponse (
        ResolvedUnityProjectContext unityProject,
        IpcResponse response,
        bool validateProjectFingerprint)
    {
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

    private static void ValidateRequest (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
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
