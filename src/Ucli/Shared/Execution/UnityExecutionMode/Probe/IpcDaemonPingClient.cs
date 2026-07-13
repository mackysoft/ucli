using System.Runtime.ExceptionServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
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
        var response = await SendPingRequestAsync(
                sessionConnection,
                Guid.NewGuid(),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        _ = DecodeResponse(unityProject, response, validateProjectFingerprint: true);
    }

    /// <inheritdoc />
    public async ValueTask PingCanonicalEndpointWithoutSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ValidateRequest(unityProject, timeout, cancellationToken);
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        // NOTE:
        // The empty token belongs only to the raw wire envelope. Receiving any valid correlated
        // response, including SESSION_TOKEN_REQUIRED, proves that the endpoint is serving IPC.
        var request = new IpcRequest(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: string.Empty,
            method: ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
            payload: IpcPayloadCodec.SerializeToElement(new IpcPingRequest(ProbeClientVersion)),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single));
        _ = await transportClient.SendAsync(
                endpoint,
                request,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PingCanonicalEndpointWithSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        IpcSessionToken sessionToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);
        ValidateRequest(unityProject, timeout, cancellationToken);
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var explicitTokenConnection = new DaemonSessionConnection(sessionToken, endpoint);
        var response = await SendPingRequestAsync(
                explicitTokenConnection,
                Guid.NewGuid(),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        _ = DecodeResponse(unityProject, response, validateProjectFingerprint: true);
    }

    /// <inheritdoc />
    public async ValueTask<IpcUnityEditorObservation> PingAndReadAsync (
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
    public async ValueTask<IpcUnityEditorObservation> PingSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        ValidateRequest(unityProject, timeout, cancellationToken);
        var sessionConnection = CreateSessionConnection(session);
        var response = await SendPingRequestAsync(
                sessionConnection,
                Guid.NewGuid(),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        return DecodeResponse(unityProject, response, validateProjectFingerprint);
    }

    private async ValueTask<IpcUnityEditorObservation> PingCurrentSessionAndReadAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var requestId = Guid.NewGuid();
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
                    requestId,
                    deadline,
                    validateProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DaemonPingResponseException exception) when (
            exception.ErrorCode == IpcSessionErrorCodes.SessionTokenInvalid)
        {
            DaemonSessionConnection refreshedSessionConnection;
            try
            {
                refreshedSessionConnection = await ResolveSessionConnectionWithinDeadlineAsync(
                        unityProject,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DaemonSessionNotAvailableException)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
                throw;
            }

            if (Equals(refreshedSessionConnection, sessionConnection))
            {
                throw;
            }

            return await SendPingAndDecodeWithinDeadlineAsync(
                    unityProject,
                    refreshedSessionConnection,
                    requestId,
                    deadline,
                    validateProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<IpcUnityEditorObservation> SendPingAndDecodeWithinDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionConnection sessionConnection,
        Guid requestId,
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
                requestId,
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
    /// <param name="requestId"> The identifier shared by every delivery attempt for the logical ping. </param>
    /// <param name="timeout"> The timeout for one ping request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the raw ping response. </returns>
    private async ValueTask<IpcResponse> SendPingRequestAsync (
        DaemonSessionConnection sessionConnection,
        Guid requestId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionConnection);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        return await transportClient.SendAsync(
                sessionConnection.Endpoint,
                UnityIpcRequestFactory.Create(
                    sessionConnection.SessionToken,
                    UnityIpcMethod.Ping,
                    IpcPayloadCodec.SerializeToElement(new IpcPingRequest(ProbeClientVersion)),
                    requestId,
                    IpcResponseMode.Single),
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
                throw new DaemonSessionNotAvailableException(sessionConnectionResult.Error!.Message);
            }

            throw new DaemonPingResponseException(
                $"Daemon session connection could not be resolved. {sessionConnectionResult.Error!.Message}");
        }

        return sessionConnectionResult.Connection!;
    }

    private static DaemonSessionConnection CreateSessionConnection (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonSessionConnection(session.SessionToken, session.Endpoint);
    }

    private static IpcUnityEditorObservation DecodeResponse (
        ResolvedUnityProjectContext unityProject,
        IpcResponse response,
        bool validateProjectFingerprint)
    {
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

    private static void ValidateRequest (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
    }

}
