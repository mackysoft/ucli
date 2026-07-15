using System.Runtime.ExceptionServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Sends daemon ping requests over IPC transport. </summary>
internal sealed class IpcDaemonPingClient : IDaemonPingClient, IDaemonPingInfoClient
{
    private const string ProbeClientVersion = "ucli-mode-probe";

    private readonly IIpcTransportClient transportClient;

    private readonly DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonPingClient" /> class. </summary>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="sessionAcquisitionCoordinator"> The coordinator that creates one acquisition scope per logical request. </param>
    /// <param name="timeProvider"> The time provider used for retry deadline accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public IpcDaemonPingClient (
        IIpcTransportClient transportClient,
        DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator,
        TimeProvider timeProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.sessionAcquisitionCoordinator = sessionAcquisitionCoordinator ?? throw new ArgumentNullException(nameof(sessionAcquisitionCoordinator));
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
        ArgumentNullException.ThrowIfNull(session);
        ValidateRequest(unityProject, timeout, cancellationToken);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var response = await SendPingRequestAsync(
                session.Endpoint,
                session.SessionToken,
                Guid.NewGuid(),
                deadline,
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
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            throw new TimeoutException("Timed out before daemon ping request could begin.");
        }

        if (!deadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
        {
            throw new TimeoutException("Timed out before daemon ping request could begin.");
        }

        // NOTE:
        // The empty token belongs only to the raw wire envelope. Receiving any valid correlated
        // response, including SESSION_TOKEN_REQUIRED, proves that the endpoint is serving IPC.
        var request = UnityIpcRequestFactory.CreateUnauthenticatedPingProbe(
            payload: IpcPayloadCodec.SerializeToElement(new IpcPingRequest(ProbeClientVersion)),
            requestId: Guid.NewGuid(),
            requestDeadlineUtc: deadline.UtcDeadline,
            requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);

        _ = await transportClient.SendAsync(
                endpoint,
                request,
                remainingTimeout,
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
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var response = await SendPingRequestAsync(
                endpoint,
                sessionToken,
                Guid.NewGuid(),
                deadline,
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
        Guid requestId,
        ExecutionDeadline deadline,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Daemon ping request identifier must not be empty.", nameof(requestId));
        }

        var response = await SendPingRequestAsync(
                session.Endpoint,
                session.SessionToken,
                requestId,
                deadline,
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
        var acquisitionScope = sessionAcquisitionCoordinator.CreateScope(deadline);
        var sessionAcquisition = await acquisitionScope.ResolveCurrentAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        DaemonPingResponseException? sessionTokenRejection = null;
        Exception? latestPreWriteFailure = null;
        while (true)
        {
            switch (sessionAcquisition.Kind)
            {
                case DaemonSessionAcquisitionKind.Success:
                    break;
                case DaemonSessionAcquisitionKind.RequestDeadlineExpired:
                    throw new TimeoutException("Timed out while resolving a daemon session for ping.");
                case DaemonSessionAcquisitionKind.PublicationWindowExpired:
                    if (sessionTokenRejection is null)
                    {
                        throw new InvalidOperationException(
                            "A daemon session publication window expired without a preceding token rejection.");
                    }

                    ExceptionDispatchInfo.Capture(sessionTokenRejection).Throw();
                    throw new InvalidOperationException("Unreachable daemon ping publication outcome.");
                case DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired:
                    if (latestPreWriteFailure is null)
                    {
                        throw new DaemonSessionNotAvailableException(
                            DaemonSessionAcquisitionResult.SessionNotAvailableMessage);
                    }

                    ExceptionDispatchInfo.Capture(latestPreWriteFailure).Throw();
                    throw new InvalidOperationException("Unreachable daemon ping endpoint outcome.");
                case DaemonSessionAcquisitionKind.SessionNotAvailable:
                    throw new DaemonSessionNotAvailableException(
                        DaemonSessionAcquisitionResult.SessionNotAvailableMessage);
                case DaemonSessionAcquisitionKind.SessionReadFailure:
                    throw new DaemonPingResponseException(
                        $"Daemon session could not be read. {sessionAcquisition.ReadFailure!.Error!.Message}");
                default:
                    throw new InvalidOperationException(
                        $"Unsupported daemon session acquisition outcome: {sessionAcquisition.Kind}.");
            }

            var session = sessionAcquisition.Session!;
            try
            {
                return await SendPingAndDecodeWithinDeadlineAsync(
                        unityProject,
                        session,
                        requestId,
                        deadline,
                        validateProjectFingerprint,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DaemonPingResponseException exception) when (
                exception.ErrorCode == IpcSessionErrorCodes.SessionTokenInvalid)
            {
                sessionTokenRejection = exception;
                latestPreWriteFailure = null;
                sessionAcquisition = await acquisitionScope.ResolveReplacementAsync(
                        unityProject,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                DaemonIpcConnectionFailureClassifier.IsRetryableBeforeRequestWrite(exception))
            {
                latestPreWriteFailure = exception;
                sessionAcquisition = await acquisitionScope.ResolveAfterPreWriteFailureAsync(
                        unityProject,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                IsRecoverableResponseInterruption(exception)
                && !deadline.IsExpired)
            {
                latestPreWriteFailure = exception;
                sessionAcquisition = await acquisitionScope.ResolveAfterStatelessResponseInterruptionAsync(
                        unityProject,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<IpcUnityEditorObservation> SendPingAndDecodeWithinDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        Guid requestId,
        ExecutionDeadline deadline,
        bool validateProjectFingerprint,
        CancellationToken cancellationToken)
    {
        var response = await SendPingRequestAsync(
                session.Endpoint,
                session.SessionToken,
                requestId,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            throw new TimeoutException("Timed out while probing the current daemon session.");
        }

        return DecodeResponse(unityProject, response, validateProjectFingerprint);
    }

    /// <summary> Sends one ping request and returns the raw IPC response envelope. </summary>
    /// <param name="endpoint"> The validated endpoint captured from one session generation. </param>
    /// <param name="sessionToken"> The authorization token captured from the same session generation. </param>
    /// <param name="requestId"> The identifier shared by every delivery attempt for the logical ping. </param>
    /// <param name="deadline"> The deadline shared by every delivery attempt for the logical ping request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the raw ping response. </returns>
    private async ValueTask<IpcResponse> SendPingRequestAsync (
        IpcEndpoint endpoint,
        IpcSessionToken sessionToken,
        Guid requestId,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(sessionToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            throw new TimeoutException("Timed out before daemon ping request could begin.");
        }

        if (!deadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
        {
            throw new TimeoutException("Timed out before daemon ping request could begin.");
        }

        var transportAttemptTimeout = remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
            ? remainingTimeout
            : DaemonTimeouts.ProbeAttemptTimeoutCap;

        return await transportClient.SendAsync(
                endpoint,
                UnityIpcRequestFactory.Create(
                    sessionToken,
                    UnityIpcMethod.Ping,
                    IpcPayloadCodec.SerializeToElement(new IpcPingRequest(ProbeClientVersion)),
                    requestId,
                    IpcResponseMode.Single,
                    deadline.UtcDeadline,
                    requestDeadlineRemainingMilliseconds),
                transportAttemptTimeout,
                cancellationToken)
            .ConfigureAwait(false);
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

    private static bool IsRecoverableResponseInterruption (Exception exception)
    {
        return exception is IpcResponseReadInterruptedException
            || exception is TimeoutException and not IpcConnectTimeoutException;
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
