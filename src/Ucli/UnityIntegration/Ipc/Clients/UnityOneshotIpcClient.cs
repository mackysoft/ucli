using System.Runtime.ExceptionServices;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Executes one IPC request through Unity oneshot batchmode startup and shared IPC transport. </summary>
internal sealed class UnityOneshotIpcClient : IUnityIpcClient
{
    private const string CleanupShutdownRequestedBy = "ucli-oneshot-cleanup";
    private const string ForceKillExitUnconfirmedDiagnostic =
        "Unity oneshot process could not be confirmed stopped after forced termination.";

    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromMilliseconds(50);

    private static readonly ProcessTerminationPolicy EmergencyTerminationPolicy = new(
        ProcessTerminationMode.GracefulThenKill,
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(10));

    private readonly IUnityBatchmodeProcessLauncher batchmodeProcessLauncher;

    private readonly IUnityIpcTransportClient transportClient;

    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    private readonly IUnityLogReader? unityLogReader;

    private readonly UnityBatchmodeProcessLifetimeOwner processLifetimeOwner = new();

    private readonly UnityOneshotCleanupPolicy cleanupPolicy;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityOneshotIpcClient" /> class. </summary>
    /// <param name="batchmodeProcessLauncher"> The Unity batchmode process launcher dependency. </param>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="unityProjectLockPreflightService"> The Unity project lock preflight service dependency. </param>
    /// <param name="unityLogReader"> The Unity log reader used for startup failure classification, or <see langword="null" /> when log classification is unavailable. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <param name="cleanupPolicy"> The validated cleanup timing policy. </param>
    public UnityOneshotIpcClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        IUnityLogReader? unityLogReader,
        TimeProvider timeProvider,
        UnityOneshotCleanupPolicy cleanupPolicy)
    {
        this.batchmodeProcessLauncher = batchmodeProcessLauncher ?? throw new ArgumentNullException(nameof(batchmodeProcessLauncher));
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.unityProjectLockPreflightService = unityProjectLockPreflightService ?? throw new ArgumentNullException(nameof(unityProjectLockPreflightService));
        this.unityLogReader = unityLogReader;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.cleanupPolicy = cleanupPolicy ?? throw new ArgumentNullException(nameof(cleanupPolicy));
    }

    /// <inheritdoc />
    public UnityExecutionTarget Target => UnityExecutionTarget.Oneshot;

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        return await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                deadline,
                dispatchObservation => SendCoreAsync(
                    unityProject,
                    dispatchRequest,
                    deadline,
                    IpcResponseMode.Single,
                    SendPreparedSingleRequestAsync,
                    dispatchObservation,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<UnityIpcReconnectAttempt> TryReconnectAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        LifecycleExecutionStartBinding requiredStart,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(requiredStart);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();
        if (dispatchRequest.RequiredStart != requiredStart)
        {
            throw new ArgumentException(
                "Oneshot reconnect requires the dispatch's authoritative start binding.",
                nameof(requiredStart));
        }
        if (ProcessLivenessProbe.ObserveIdentity(
                requiredStart.Host.Process)
            == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
        {
            return UnityIpcReconnectAttempt.Owned(
                CreateConfirmedHostExitResult(
                    requiredStart,
                    lifecycleActionDispatched: false));
        }

        var candidates =
            OneshotBootstrapEnvelopeStore
                .ReadLifecycleReconnectCandidates(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    timeProvider.GetUtcNow());
        if (candidates.Count == 0)
        {
            return UnityIpcReconnectAttempt.NotOwned();
        }

        var probe = await ProbeReconnectCandidatesAsync(
                unityProject,
                dispatchRequest,
                requiredStart,
                candidates,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (probe.Result is not null)
        {
            return UnityIpcReconnectAttempt.Owned(probe.Result);
        }
        if (probe.Envelope is null)
        {
            return UnityIpcReconnectAttempt.NotOwned();
        }

        var result = await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                deadline,
                dispatchObservation =>
                    SendExistingLifecycleExecutionAsync(
                        unityProject,
                        probe.Envelope.SessionToken,
                        dispatchRequest,
                        deadline,
                        dispatchObservation),
                cancellationToken)
            .ConfigureAwait(false);
        return UnityIpcReconnectAttempt.Owned(result);
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendStreamingAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(deadline);
        if (!UnityIpcMethodCapabilities.SupportsStreaming(dispatchRequest.Method))
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                $"IPC method does not support streaming: {TextVocabulary.GetText(dispatchRequest.Method)}."));
        }

        return await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                deadline,
                dispatchObservation => SendCoreAsync(
                    unityProject,
                    dispatchRequest,
                    deadline,
                    IpcResponseMode.Stream,
                    (preparedUnityProject, request, requestTimeout, requestCancellationToken) =>
                        SendPreparedStreamingRequestAsync(
                            preparedUnityProject,
                            request,
                            requestTimeout,
                            cancellationToken.IsCancellationRequested
                                ? static (_, _) => ValueTask.CompletedTask
                                : onProgressFrame,
                            requestCancellationToken),
                    dispatchObservation,
                    cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityRequestExecutionResult> SendCoreAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        IpcResponseMode responseMode,
        Func<
            ResolvedUnityProjectContext,
            IpcRequestEnvelope,
            TimeSpan,
            CancellationToken,
            ValueTask<IpcResponse>> sendPreparedRequestAsync,
        LifecycleExecutionDispatchObservation? dispatchObservation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(sendPreparedRequestAsync);
        cancellationToken.ThrowIfCancellationRequested();
        var dispatchCancellationToken = cancellationToken;

        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        try
        {
            if (!deadline.TryGetRemainingTimeout(out var lockTimeout))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.OneshotTimeout(deadline.Timeout));
            }

            await using var lifecycleLock = new BestEffortAsyncDisposable(
                await lifecycleLockProvider.AcquireAsync(
                        new ProjectLifecycleLockRequest(unityProject.UnityProjectRoot),
                        lockTimeout,
                        dispatchCancellationToken)
                    .ConfigureAwait(false));

            if (unityLogPath.TryGetParent(out var unityLogDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(unityLogDirectoryPath);
            }

            if (!TryGetDispatchBudget(
                    deadline,
                    out _,
                    out _,
                    out _))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.OneshotTimeout(deadline.Timeout));
            }

            var sessionToken = IpcSessionToken.CreateRandom();
            var bootstrapCreatedAtUtc = timeProvider.GetUtcNow();
            var hardExitDeadlineUtc =
                dispatchRequest.BeginsLifecycleExecution
                    ? deadline.CreateCompletionDeadline(
                            LifecycleExecutionTiming.ResponseDeliveryGrace)
                        .UtcDeadline
                    : deadline.UtcDeadline;
            var bootstrapEnvelope = new IpcOneshotBootstrapEnvelope(
                BootstrapId: Guid.NewGuid(),
                ParentProcess: ProcessLivenessProbe.CaptureCurrentProcess(),
                ProjectFingerprint: unityProject.ProjectFingerprint,
                SessionToken: sessionToken,
                CreatedAtUtc: bootstrapCreatedAtUtc,
                ExitDeadlineUtc: hardExitDeadlineUtc,
                Endpoint: endpoint.Contract);
            var launchResult = await batchmodeProcessLauncher.LaunchOneshotAsync(
                    unityProject,
                    bootstrapEnvelope,
                    unityLogPath,
                    dispatchRequest.LaunchOptions,
                    dispatchCancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromExecutionError(launchResult.Error!));
            }

            var processHandle = launchResult.ProcessHandle!;
            await using var processHandleDisposal = new BestEffortAsyncDisposable(processHandle);
            var shouldTerminateProcess = true;
            var terminationResult = ProcessTerminationResult.None;
            Exception? processCleanupException = null;
            LifecycleExecutionStartBinding? lifecycleExecutionStart = null;
            var lifecycleActionDispatched = false;
            UnityRequestExecutionResult? result = null;

            void PreserveRunningLifecycleProcess ()
            {
                if (lifecycleExecutionStart == null
                    || processHandle.HasExited
                    || !shouldTerminateProcess)
                {
                    return;
                }

                processLifetimeOwner.Transfer(processHandle);
                processHandleDisposal.RelinquishOwnership();
                shouldTerminateProcess = false;
            }

            try
            {
                while (result == null)
                {
                    var startupProbeFailure = await WaitUntilReachableAsync(
                        unityProject,
                        sessionToken,
                        dispatchRequest,
                        ResolveStartupProbeFailFast(dispatchRequest),
                        deadline,
                        processHandle,
                        dispatchCancellationToken)
                    .ConfigureAwait(false);
                    if (startupProbeFailure != null)
                    {
                        result = UnityRequestExecutionResult.Failure(startupProbeFailure);
                    }
                    else if (!TryGetDispatchBudget(
                            deadline,
                            out _,
                            out _,
                            out _))
                    {
                        result = UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.OneshotTimeout(deadline.Timeout));
                    }
                    else
                    {
                        var preparedDispatchCancellationToken =
                            dispatchRequest.Registration != null
                                ? CancellationToken.None
                                : dispatchCancellationToken;

                        var dispatchOutcome = await SendPreparedDispatchAsync(
                                unityProject,
                                sessionToken,
                                dispatchRequest,
                                deadline,
                                responseMode,
                                processHandle,
                                sendPreparedRequestAsync,
                                async confirmedStart =>
                                {
                                    lifecycleExecutionStart = confirmedStart;
                                    return await ObserveStartAsync(
                                            dispatchRequest,
                                            confirmedStart,
                                            deadline,
                                            dispatchObservation)
                                        .ConfigureAwait(false);
                                },
                                () =>
                                {
                                    lifecycleActionDispatched = true;
                                    dispatchObservation?.ReportActionDispatched();
                                },
                                preparedDispatchCancellationToken)
                            .ConfigureAwait(false);
                        lifecycleExecutionStart = dispatchOutcome.LifecycleExecutionStart;
                        if (lifecycleExecutionStart is null
                            && dispatchRequest.Registration is not null)
                        {
                            lifecycleExecutionStart =
                                await LifecycleExecutionStartRecordRecovery
                                    .TryReadAsync(
                                        unityProject,
                                        dispatchRequest)
                                    .ConfigureAwait(false);
                            if (lifecycleExecutionStart is not null)
                            {
                                var startObservation = await ObserveStartAsync(
                                        dispatchRequest,
                                        lifecycleExecutionStart,
                                        deadline,
                                        dispatchObservation)
                                    .ConfigureAwait(false);
                                if (startObservation is not null)
                                {
                                    result = UnityRequestExecutionResult.Failure(
                                        startObservation,
                                        lifecycleExecutionStart);
                                    break;
                                }
                            }
                        }

                        lifecycleActionDispatched =
                            lifecycleExecutionStart != null
                            && dispatchOutcome.ActionDispatched;
                        if (ShouldRetryRejectedStart(
                                dispatchRequest,
                                dispatchOutcome))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            continue;
                        }

                        if (dispatchOutcome.Failure != null)
                        {
                            PreserveRunningLifecycleProcess();
                            result = UnityRequestExecutionResult.Failure(
                                dispatchOutcome.Failure,
                                lifecycleExecutionStart,
                                lifecycleActionDispatched);
                        }
                        else if (dispatchOutcome.Response == null)
                        {
                            throw new InvalidOperationException(
                                "Lifecycle Execution dispatch finished without a response or classified failure.");
                        }
                        else
                        {
                            var responseResult = UnityRequestExecutionResult.Success(
                                UnityRequestResponseFactory.Create(dispatchOutcome.Response),
                                lifecycleExecutionStart,
                                lifecycleActionDispatched);
                            var retainsNonTerminalLifecycleExecution =
                                lifecycleExecutionStart is not null
                                && dispatchOutcome.ActionDispatched
                                && !await IsDurablyTerminalAsync(
                                        unityProject,
                                        lifecycleExecutionStart)
                                    .ConfigureAwait(false);
                            if (retainsNonTerminalLifecycleExecution)
                            {
                                PreserveRunningLifecycleProcess();
                                result = responseResult;
                                continue;
                            }

                            var terminalPingShutdownError = dispatchOutcome.ActionDispatched
                                ? await RequestTerminalPingShutdownAsync(
                                        unityProject,
                                        sessionToken,
                                        dispatchRequest,
                                        processHandle)
                                    .ConfigureAwait(false)
                                : null;
                            if (terminalPingShutdownError != null)
                            {
                                result = UnityRequestExecutionResult.Failure(
                                    UnityIpcFailureClassifier.FromExecutionError(
                                        terminalPingShutdownError),
                                    lifecycleExecutionStart,
                                    lifecycleActionDispatched);
                            }
                            else if (!dispatchOutcome.ActionDispatched)
                            {
                                result = responseResult;
                            }
                            else
                            {
                                try
                                {
                                    var exitWaitError = await WaitForExitAsync(
                                            processHandle,
                                            cleanupPolicy.Timeout,
                                            timeProvider,
                                            dispatchCancellationToken)
                                        .ConfigureAwait(false);
                                    if (exitWaitError == null)
                                    {
                                        shouldTerminateProcess = false;
                                        result = responseResult;
                                    }
                                    else if (IsCommandResponseBoundary(dispatchRequest))
                                    {
                                        result = responseResult;
                                    }
                                    else
                                    {
                                        result = UnityRequestExecutionResult.Failure(
                                            UnityIpcFailureClassifier.FromExecutionError(
                                                exitWaitError),
                                            lifecycleExecutionStart,
                                            lifecycleActionDispatched);
                                    }
                                }
                                catch (Exception) when (IsCommandResponseBoundary(dispatchRequest))
                                {
                                    result = responseResult;
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (dispatchCancellationToken.IsCancellationRequested)
            {
                PreserveRunningLifecycleProcess();
                throw;
            }
            catch (IpcProgressFrameHandlerException)
            {
                PreserveRunningLifecycleProcess();
                throw;
            }
            catch (Exception exception)
            {
                PreserveRunningLifecycleProcess();
                result = UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromOneshotDispatchException(exception, deadline.Timeout),
                    lifecycleExecutionStart,
                    lifecycleActionDispatched);
            }
            finally
            {
                if (shouldTerminateProcess)
                {
                    try
                    {
                        if (!processHandle.HasExited)
                        {
                            terminationResult = await CleanupLaunchedProcessAsync(
                                    unityProject,
                                    sessionToken,
                                    processHandle)
                                .ConfigureAwait(false);
                        }
                    }
                    catch (Exception exception)
                    {
                        processCleanupException = exception;
                    }

                    if (terminationResult == ProcessTerminationResult.ForceKillFailed
                        || processCleanupException is not null)
                    {
                        try
                        {
                            processLifetimeOwner.Transfer(processHandle);
                            processHandleDisposal.RelinquishOwnership();
                        }
                        catch (Exception exception)
                        {
                            processCleanupException ??= exception;
                        }
                    }
                }
            }

            result ??= UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.InternalError(
                    "Unity oneshot dispatch finished without a classified result."));
            result = result.WithLifecycleExecutionStart(
                lifecycleExecutionStart,
                lifecycleActionDispatched);
            if (processCleanupException is not null && !result.IsSuccess)
            {
                result = AppendNonRecoverableProcessCleanupDiagnostic(
                    result,
                    $"Unity oneshot process cleanup did not complete. {processCleanupException.Message}");
            }

            return await AppendPostTerminationDiagnosticAsync(result, terminationResult, unityProject).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (dispatchCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IpcProgressFrameHandlerException exception)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }
        catch (Exception exception)
        {
            return UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.FromOneshotDispatchException(exception, deadline.Timeout));
        }
    }

    private async ValueTask<OneshotReconnectProbeOutcome>
        ProbeReconnectCandidatesAsync (
            ResolvedUnityProjectContext unityProject,
            UnityIpcDispatchRequest dispatchRequest,
            LifecycleExecutionStartBinding requiredStart,
            IReadOnlyList<IpcOneshotBootstrapEnvelope> candidates,
            ExecutionDeadline deadline,
            CancellationToken cancellationToken)
    {
        while (true)
        {
            if (ProcessLivenessProbe.ObserveIdentity(
                    requiredStart.Host.Process)
                == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
            {
                return OneshotReconnectProbeOutcome.Completed(
                    CreateConfirmedHostExitResult(
                        requiredStart,
                        lifecycleActionDispatched: false));
            }

            var retryAfterEndpointInterruption = false;
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetDispatchBudget(
                        deadline,
                        out var requestTimeout,
                        out var requestDeadlineRemainingMilliseconds,
                        out var requestDeadlineUtc))
                {
                    return OneshotReconnectProbeOutcome.Completed(
                        UnityRequestExecutionResult.Failure(
                            UnityIpcFailureClassifier.OneshotTimeout(
                                deadline.Timeout),
                            requiredStart));
                }

                IpcResponse response;
                try
                {
                    response = await transportClient.SendAsync(
                            unityProject.RepositoryRoot,
                            unityProject.ProjectFingerprint,
                            LifecycleExecutionStartExchange.CreateRequest(
                                dispatchRequest,
                                candidate.SessionToken,
                                Guid.NewGuid(),
                                requestDeadlineUtc,
                                requestDeadlineRemainingMilliseconds),
                            requestTimeout,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (
                    IsStartupRetryable(exception))
                {
                    retryAfterEndpointInterruption = true;
                    continue;
                }

                if (IsSessionTokenInvalid(response))
                {
                    continue;
                }
                switch (LifecycleExecutionStartExchange.InterpretResponse(
                    dispatchRequest,
                    response))
                {
                    case LifecycleExecutionStartExchange
                        .ProviderRejected rejected:
                        return OneshotReconnectProbeOutcome.Completed(
                            UnityRequestExecutionResult.Success(
                                UnityRequestResponseFactory.Create(
                                    rejected.Response),
                                requiredStart));
                    case LifecycleExecutionStartExchange.Invalid invalid:
                        return OneshotReconnectProbeOutcome.Completed(
                            UnityRequestExecutionResult.Failure(
                                invalid.Failure,
                                requiredStart));
                    case LifecycleExecutionStartExchange.Mismatched mismatched
                        when mismatched.Code
                            == LifecycleExecutionErrorCodes.HostMismatch
                            || mismatched.Code
                            == LifecycleExecutionErrorCodes.ProjectMismatch:
                        continue;
                    case LifecycleExecutionStartExchange.Mismatched mismatched:
                        return OneshotReconnectProbeOutcome.Completed(
                            UnityRequestExecutionResult.Failure(
                                UnityIpcFailureClassifier.FromCodeAndMessage(
                                    mismatched.Code,
                                    "The oneshot provider returned a Lifecycle Execution start with a mismatched generation."),
                                requiredStart));
                    case LifecycleExecutionStartExchange.Confirmed:
                        return OneshotReconnectProbeOutcome.Owned(candidate);
                    default:
                        throw new InvalidOperationException(
                            "Unsupported Lifecycle Execution start interpretation.");
                }
            }

            if (!retryAfterEndpointInterruption)
            {
                return OneshotReconnectProbeOutcome.NotOwned();
            }
            if (ProcessLivenessProbe.ObserveIdentity(
                    requiredStart.Host.Process)
                == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
            {
                return OneshotReconnectProbeOutcome.Completed(
                    CreateConfirmedHostExitResult(
                        requiredStart,
                        lifecycleActionDispatched: false));
            }
            if (!deadline.TryGetRemainingTimeout(
                    out var remainingTimeout))
            {
                return OneshotReconnectProbeOutcome.Completed(
                    UnityRequestExecutionResult.Failure(
                        UnityIpcFailureClassifier.OneshotTimeout(
                            deadline.Timeout),
                        requiredStart));
            }

            await TimeProviderDelay.DelayAsync(
                    GetRetryDelay(remainingTimeout),
                    timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<UnityRequestExecutionResult>
        SendExistingLifecycleExecutionAsync (
            ResolvedUnityProjectContext unityProject,
            IpcSessionToken sessionToken,
            UnityIpcDispatchRequest dispatchRequest,
            ExecutionDeadline deadline,
            LifecycleExecutionDispatchObservation? dispatchObservation)
    {
        var dispatchOutcome = await SendPreparedDispatchAsync(
                unityProject,
                sessionToken,
                dispatchRequest,
                deadline,
                IpcResponseMode.Single,
                processHandle: null,
                SendPreparedSingleRequestAsync,
                dispatchObservation is null
                    ? null
                    : start =>
                    {
                        dispatchObservation.ReportStarted(start);
                        return ValueTask.FromResult<UnityRequestFailure?>(null);
                    },
                dispatchObservation is null
                    ? null
                    : dispatchObservation.ReportActionDispatched,
                CancellationToken.None)
            .ConfigureAwait(false);
        var retainedStart = dispatchOutcome.LifecycleExecutionStart
            ?? dispatchRequest.RequiredStart;
        if (dispatchOutcome.Failure is not null)
        {
            return UnityRequestExecutionResult.Failure(
                dispatchOutcome.Failure,
                retainedStart,
                dispatchOutcome.ActionDispatched,
                dispatchOutcome.ConfirmedHostExit);
        }
        if (dispatchOutcome.Response is null)
        {
            return UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.InternalError(
                    "Oneshot Lifecycle Execution reconnect finished without a response or classified failure."),
                retainedStart,
                dispatchOutcome.ActionDispatched);
        }

        return UnityRequestExecutionResult.Success(
            UnityRequestResponseFactory.Create(
                dispatchOutcome.Response),
            retainedStart,
            dispatchOutcome.ActionDispatched);
    }

    private async ValueTask<OneshotPreparedDispatchOutcome> SendPreparedDispatchAsync (
        ResolvedUnityProjectContext unityProject,
        IpcSessionToken sessionToken,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        IpcResponseMode responseMode,
        IUnityBatchmodeProcessHandle? processHandle,
        Func<
            ResolvedUnityProjectContext,
            IpcRequestEnvelope,
            TimeSpan,
            CancellationToken,
            ValueTask<IpcResponse>> sendPreparedRequestAsync,
        Func<LifecycleExecutionStartBinding, ValueTask<UnityRequestFailure?>>? lifecycleStarted,
        Action? lifecycleActionDispatched,
        CancellationToken cancellationToken)
    {
        var lifecycleStartRequestId = Guid.NewGuid();
        var actionRequestId = Guid.NewGuid();
        var actionWasDispatched = false;
        var lifecycleStartReported = false;
        LifecycleExecutionStartBinding? lifecycleExecutionStart =
            dispatchRequest.RequiredStart;
        var dispatchDeadline = deadline;
        var lifecycleCompletionDeadlineStarted = false;
        if (lifecycleExecutionStart is not null)
        {
            var requiredStartObservation = lifecycleStarted is null
                ? null
                : await lifecycleStarted(lifecycleExecutionStart)
                    .ConfigureAwait(false);
            if (requiredStartObservation is not null)
            {
                return OneshotPreparedDispatchOutcome.Failed(
                    requiredStartObservation,
                    lifecycleExecutionStart,
                    actionWasDispatched);
            }
            lifecycleStartReported = true;
        }

        while (true)
        {
            if (dispatchRequest.RequiredStart is not null
                && ProcessLivenessProbe.ObserveIdentity(
                    dispatchRequest.RequiredStart.Host.Process)
                == ProcessIdentityObservation.ConfirmedExitedOrReplaced)
            {
                return OneshotPreparedDispatchOutcome.Failed(
                    UnityIpcFailureClassifier.FromCodeAndMessage(
                        EditorLifecycleErrorCodes.EditorUnavailable,
                        "The Unity Editor process that owns the Lifecycle Execution exited during reconnect."),
                    lifecycleExecutionStart,
                    actionWasDispatched,
                    new LifecycleExecutionHostExitObservation(
                        dispatchRequest.RequiredStart.Host.Process));
            }

            if (!TryGetDispatchBudget(
                    dispatchDeadline,
                    out var requestTimeout,
                    out var requestDeadlineRemainingMilliseconds,
                    out var requestDeadlineUtc))
            {
                return OneshotPreparedDispatchOutcome.Failed(
                    UnityIpcFailureClassifier.OneshotTimeout(
                        dispatchDeadline.Timeout),
                    lifecycleExecutionStart,
                    actionWasDispatched);
            }

            try
            {
                var actionPayload = dispatchRequest.Registration == null
                    ? dispatchRequest.Payload
                    : default;
                if (dispatchRequest.Registration != null)
                {
                    if (dispatchRequest.BeginsLifecycleExecution
                        && !lifecycleCompletionDeadlineStarted)
                    {
                        // The first Start write is the ambiguity boundary after which a durable
                        // Start may exist even if its response is lost. Preserve only the
                        // terminal-publication and response-delivery grace from this point.
                        dispatchDeadline = deadline.CreateCompletionDeadline(
                            LifecycleExecutionTiming.ResponseDeliveryGrace);
                        lifecycleCompletionDeadlineStarted = true;
                        if (!TryGetDispatchBudget(
                                dispatchDeadline,
                                out requestTimeout,
                                out requestDeadlineRemainingMilliseconds,
                                out requestDeadlineUtc))
                        {
                            return OneshotPreparedDispatchOutcome.Failed(
                                UnityIpcFailureClassifier.OneshotTimeout(
                                    dispatchDeadline.Timeout),
                                lifecycleExecutionStart,
                                actionWasDispatched);
                        }
                    }

                    var lifecycleStartResponse = await transportClient.SendAsync(
                            unityProject.RepositoryRoot,
                            unityProject.ProjectFingerprint,
                            LifecycleExecutionStartExchange.CreateRequest(
                                dispatchRequest,
                                sessionToken,
                                lifecycleStartRequestId,
                                requestDeadlineUtc,
                                requestDeadlineRemainingMilliseconds),
                            requestTimeout,
                            cancellationToken)
                        .ConfigureAwait(false);
                    switch (LifecycleExecutionStartExchange
                        .InterpretResponse(
                            dispatchRequest,
                            lifecycleStartResponse))
                    {
                        case LifecycleExecutionStartExchange
                            .ProviderRejected rejected:
                            return OneshotPreparedDispatchOutcome.Responded(
                                rejected.Response,
                                lifecycleExecutionStart,
                                actionWasDispatched);
                        case LifecycleExecutionStartExchange.Invalid invalid:
                            return OneshotPreparedDispatchOutcome.Failed(
                                invalid.Failure,
                                lifecycleExecutionStart,
                                actionWasDispatched);
                        case LifecycleExecutionStartExchange
                            .Mismatched mismatched:
                            return OneshotPreparedDispatchOutcome.Failed(
                                UnityIpcFailureClassifier.FromCodeAndMessage(
                                    mismatched.Code,
                                    "The oneshot provider returned a Lifecycle Execution start that does not match the authoritative persisted start."),
                                lifecycleExecutionStart,
                                actionWasDispatched);
                        case LifecycleExecutionStartExchange.Confirmed confirmed:
                            actionPayload = confirmed.ActionPayload;
                            lifecycleExecutionStart = confirmed.Start;
                            if (!lifecycleStartReported)
                            {
                                var startObservation = lifecycleStarted is null
                                    ? null
                                    : await lifecycleStarted(confirmed.Start)
                                        .ConfigureAwait(false);
                                if (startObservation is not null)
                                {
                                    return OneshotPreparedDispatchOutcome.Failed(
                                        startObservation,
                                        lifecycleExecutionStart,
                                        actionWasDispatched);
                                }
                                lifecycleStartReported = true;
                            }
                            break;
                        default:
                            throw new InvalidOperationException(
                                "Unsupported Lifecycle Execution start interpretation.");
                    }

                    if (!TryGetDispatchBudget(
                            dispatchDeadline,
                            out requestTimeout,
                            out requestDeadlineRemainingMilliseconds,
                            out requestDeadlineUtc))
                    {
                        return OneshotPreparedDispatchOutcome.Failed(
                            UnityIpcFailureClassifier.OneshotTimeout(
                                dispatchDeadline.Timeout),
                            lifecycleExecutionStart,
                            actionWasDispatched);
                    }
                }

                var request = UnityIpcRequestFactory.Create(
                    sessionToken,
                    dispatchRequest.Method,
                    actionPayload,
                    actionRequestId,
                    responseMode,
                    requestDeadlineUtc,
                    requestDeadlineRemainingMilliseconds);
                if (lifecycleExecutionStart != null)
                {
                    lifecycleActionDispatched?.Invoke();
                }

                actionWasDispatched = true;
                var responseAttempt = sendPreparedRequestAsync(
                        unityProject,
                        request,
                        requestTimeout,
                        cancellationToken);
                var response = await responseAttempt.ConfigureAwait(false);
                return OneshotPreparedDispatchOutcome.Responded(
                    response,
                    lifecycleExecutionStart,
                    actionWasDispatched);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (IpcProgressFrameHandlerException)
            {
                throw;
            }
            catch (Exception exception) when (
                dispatchRequest.Registration != null
                && IsStartupRetryable(exception))
            {
                if (lifecycleExecutionStart is null)
                {
                    lifecycleExecutionStart =
                        await LifecycleExecutionStartRecordRecovery.TryReadAsync(
                                unityProject,
                                dispatchRequest)
                            .ConfigureAwait(false);
                    if (lifecycleExecutionStart is not null
                        && !lifecycleStartReported)
                    {
                        var startObservation = lifecycleStarted is null
                            ? null
                            : await lifecycleStarted(lifecycleExecutionStart)
                                .ConfigureAwait(false);
                        if (startObservation is not null)
                        {
                            return OneshotPreparedDispatchOutcome.Failed(
                                startObservation,
                                lifecycleExecutionStart,
                                actionWasDispatched);
                        }
                        lifecycleStartReported = true;
                    }
                }

                if (dispatchDeadline.IsExpired)
                {
                    return OneshotPreparedDispatchOutcome.Failed(
                        UnityIpcFailureClassifier.OneshotTimeout(
                            dispatchDeadline.Timeout),
                        lifecycleExecutionStart,
                        actionWasDispatched);
                }

                if (processHandle is null)
                {
                    if (dispatchRequest.RequiredStart is not null
                        && ProcessLivenessProbe.ObserveIdentity(
                            dispatchRequest.RequiredStart.Host.Process)
                        == ProcessIdentityObservation
                            .ConfirmedExitedOrReplaced)
                    {
                        return OneshotPreparedDispatchOutcome.Failed(
                            UnityIpcFailureClassifier.FromCodeAndMessage(
                                EditorLifecycleErrorCodes.EditorUnavailable,
                                "The Unity Editor process that owns the Lifecycle Execution exited during reconnect."),
                            lifecycleExecutionStart,
                            actionWasDispatched,
                            new LifecycleExecutionHostExitObservation(
                                dispatchRequest.RequiredStart.Host.Process));
                    }

                    if (!dispatchDeadline.TryGetRemainingTimeout(
                            out var reconnectRemainingTimeout))
                    {
                        return OneshotPreparedDispatchOutcome.Failed(
                            UnityIpcFailureClassifier.OneshotTimeout(
                                dispatchDeadline.Timeout),
                            lifecycleExecutionStart,
                            actionWasDispatched);
                    }

                    await TimeProviderDelay.DelayAsync(
                            GetRetryDelay(reconnectRemainingTimeout),
                            timeProvider,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    continue;
                }

                var reachabilityFailure = await WaitUntilReachableAsync(
                        unityProject,
                        sessionToken,
                        dispatchRequest,
                        failFast: false,
                        dispatchDeadline,
                        processHandle,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (reachabilityFailure != null)
                {
                    return OneshotPreparedDispatchOutcome.Failed(
                        reachabilityFailure,
                        lifecycleExecutionStart,
                        actionWasDispatched);
                }
            }
        }
    }

    private static async ValueTask<UnityRequestFailure?> ObserveStartAsync (
        UnityIpcDispatchRequest dispatchRequest,
        LifecycleExecutionStartBinding start,
        ExecutionDeadline deadline,
        LifecycleExecutionDispatchObservation? dispatchObservation)
    {
        var observation = await dispatchRequest
            .ObserveLifecycleStartAsync(start)
            .ConfigureAwait(false);
        if (observation is LifecycleExecutionStartObservation.Rejected rejected)
        {
            return UnityIpcFailureClassifier.FromCodeAndMessage(
                rejected.Failure.Code,
                rejected.Failure.Message);
        }

        if (dispatchRequest.LifecycleStartObserver is not null
            && deadline.IsExpired)
        {
            return UnityIpcFailureClassifier.Timeout(
                "Lifecycle Execution deadline expired while its durable start was being recorded.");
        }

        dispatchObservation?.ReportStarted(start);
        return null;
    }

    private ValueTask<IpcResponse> SendPreparedSingleRequestAsync (
        ResolvedUnityProjectContext unityProject,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return transportClient.SendAsync(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            request,
            timeout,
            cancellationToken);
    }

    private ValueTask<IpcResponse> SendPreparedStreamingRequestAsync (
        ResolvedUnityProjectContext unityProject,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken)
    {
        return transportClient.SendStreamingAsync(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            request,
            timeout,
            onProgressFrame,
            cancellationToken);
    }

    private static bool IsCommandResponseBoundary (UnityIpcDispatchRequest dispatchRequest)
    {
        // NOTE: A non-ping response is the command contract boundary; delayed Unity process exit is cleanup work.
        return dispatchRequest.Method != UnityIpcMethod.Ping;
    }

    private static async ValueTask<bool> IsDurablyTerminalAsync (
        ResolvedUnityProjectContext unityProject,
        LifecycleExecutionStartBinding start)
    {
        try
        {
            var expectedReference = start.LifecycleExecutionRef;
            var executionKind = LifecycleExecutionContractGuard.RequireReference(
                expectedReference,
                nameof(start),
                allowTerminal: false);
            var store = FileLifecycleExecutionStore.CreateForProject(
                unityProject.UnityProjectRoot,
                unityProject.ProjectFingerprint);
            var stored = await store.ReadAsync(
                    executionKind,
                    expectedReference.Id,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (stored?.IsTerminal != true)
            {
                return false;
            }

            var currentReference = stored.CurrentReference;
            return currentReference.Kind == expectedReference.Kind
                && currentReference.Id == expectedReference.Id
                && currentReference.DefinitionDigest
                    == expectedReference.DefinitionDigest
                && currentReference.StatusLocator
                    == expectedReference.StatusLocator;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // The process remains the only host allowed to recover this execution.
            // An unreadable status record is not proof that its terminal record was published.
            return false;
        }
    }

    private async ValueTask<ExecutionError?> RequestTerminalPingShutdownAsync (
        ResolvedUnityProjectContext unityProject,
        IpcSessionToken sessionToken,
        UnityIpcDispatchRequest dispatchRequest,
        IUnityBatchmodeProcessHandle processHandle)
    {
        if (dispatchRequest.Method != UnityIpcMethod.Ping
            || processHandle.HasExited)
        {
            return null;
        }

        var cleanupDeadline = ExecutionDeadline.Start(cleanupPolicy.Timeout, timeProvider);
        var shutdownRequestId = Guid.NewGuid();
        if (await TryRequestShutdownUntilCleanupDeadlineAsync(
                unityProject,
                sessionToken,
                shutdownRequestId,
                processHandle,
                cleanupDeadline)
            .ConfigureAwait(false))
        {
            return null;
        }

        return ExecutionError.Timeout(
            $"Unity oneshot ping shutdown did not complete within {cleanupPolicy.Timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout);
    }

    private async ValueTask<ProcessTerminationResult> CleanupLaunchedProcessAsync (
        ResolvedUnityProjectContext unityProject,
        IpcSessionToken sessionToken,
        IUnityBatchmodeProcessHandle processHandle)
    {
        if (processHandle.HasExited)
        {
            return ProcessTerminationResult.None;
        }

        var cleanupDeadline = ExecutionDeadline.Start(cleanupPolicy.Timeout, timeProvider);
        var shutdownRequestId = Guid.NewGuid();
        if (await TryRequestShutdownUntilCleanupDeadlineAsync(
                unityProject,
                sessionToken,
                shutdownRequestId,
                processHandle,
                cleanupDeadline)
            .ConfigureAwait(false)
            && !processHandle.HasExited
            && cleanupDeadline.TryGetRemainingTimeout(out var exitTimeout))
        {
            var exitWaitError = await WaitForExitAsync(processHandle, exitTimeout, timeProvider, CancellationToken.None).ConfigureAwait(false);
            if (exitWaitError == null || processHandle.HasExited)
            {
                return ProcessTerminationResult.None;
            }
        }

        if (processHandle.HasExited)
        {
            return ProcessTerminationResult.None;
        }

        return await processHandle.TerminateAsync(
                EmergencyTerminationPolicy,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async ValueTask<bool> TryRequestShutdownUntilCleanupDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        IpcSessionToken sessionToken,
        Guid shutdownRequestId,
        IUnityBatchmodeProcessHandle processHandle,
        ExecutionDeadline cleanupDeadline)
    {
        while (!processHandle.HasExited)
        {
            if (!cleanupDeadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return false;
            }

            try
            {
                var attemptTimeout = GetCleanupAttemptTimeout(remainingTimeout);
                if (!cleanupDeadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
                {
                    return false;
                }

                var shutdownRequest = CreateShutdownRequest(
                    sessionToken,
                    shutdownRequestId,
                    cleanupDeadline.UtcDeadline,
                    requestDeadlineRemainingMilliseconds);
                var response = await transportClient.SendAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        shutdownRequest,
                        attemptTimeout,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (!IpcResponseFailureReader.TryRead(response, out var firstError))
                {
                    return true;
                }

                if (!IsCleanupShutdownResponseRetryable(firstError))
                {
                    return false;
                }

                if (!cleanupDeadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return false;
                }

                await TimeProviderDelay.DelayAsync(
                        GetCleanupRetryDelay(remainingTimeout),
                        timeProvider,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (IsCleanupShutdownRetryable(exception))
            {
                if (!cleanupDeadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return false;
                }

                await TimeProviderDelay.DelayAsync(GetCleanupRetryDelay(remainingTimeout), timeProvider, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary> Waits until the launched oneshot Unity process accepts the startup probe request. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="sessionToken"> The canonical session token assigned to the launched oneshot process. </param>
    /// <param name="failFast"> Whether readiness probing should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="deadline"> The shared request deadline. </param>
    /// <param name="processHandle"> The launched process handle. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> <see langword="null" /> when startup is reachable; otherwise the startup failure. </returns>
    private async ValueTask<UnityRequestFailure?> WaitUntilReachableAsync (
        ResolvedUnityProjectContext unityProject,
        IpcSessionToken sessionToken,
        UnityIpcDispatchRequest dispatchRequest,
        bool failFast,
        ExecutionDeadline deadline,
        IUnityBatchmodeProcessHandle processHandle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        var startupProbeRequestId = Guid.NewGuid();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                var message = $"Unity oneshot IPC request timed out after {deadline.Timeout.TotalMilliseconds:0} milliseconds.";
                return await CreateStartupTimeoutFailureAsync(
                        unityProject,
                        processHandle,
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (processHandle.HasExited)
            {
                var exitCode = processHandle.ExitCode;
                var message = exitCode is int code
                    ? $"Unity oneshot process exited before startup readiness was confirmed. ExitCode={code}."
                    : "Unity oneshot process exited before startup readiness was confirmed.";
                message = await AppendPostUnityProcessExitLockFileDiagnosticAsync(message, unityProject).ConfigureAwait(false);
                return await CreateStartupProcessExitFailureAsync(
                        unityProject,
                        processHandle,
                        message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var attemptTimeout = remainingTimeout < TimeSpan.FromSeconds(1)
                ? remainingTimeout
                : TimeSpan.FromSeconds(1);
            try
            {
                if (!deadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
                {
                    var message = $"Unity oneshot IPC request timed out after {deadline.Timeout.TotalMilliseconds:0} milliseconds.";
                    return await CreateStartupTimeoutFailureAsync(
                            unityProject,
                            processHandle,
                            message,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                var startupProbeRequest = CreateStartupProbeRequest(
                    sessionToken,
                    startupProbeRequestId,
                    deadline.UtcDeadline,
                    requestDeadlineRemainingMilliseconds);
                var pingResponse = await transportClient.SendAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        startupProbeRequest,
                        attemptTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!DaemonPingResponseCodec.TryDecodePayloadForProject(
                        pingResponse,
                        unityProject.ProjectFingerprint,
                        "Unity oneshot startup probe",
                        out var payload,
                        out var error))
                {
                    return UnityIpcFailureClassifier.InternalError(
                        $"Unity oneshot startup probe returned an invalid response. {error!.Message}");
                }

                if (dispatchRequest.RequiredStart is not null)
                {
                    return null;
                }

                var readinessDecision = dispatchRequest.StartAdmissionPolicy
                    ?.Evaluate(payload!)
                    ?? UnityEditorReadinessPolicy.Evaluate(
                        payload!,
                        failFast);
                if (readinessDecision.IsReady)
                {
                    return null;
                }

                if (IsStartupLifecycleDispatchAllowed(dispatchRequest, payload!))
                {
                    return null;
                }

                if (readinessDecision.IsFailure)
                {
                    return UnityIpcFailureClassifier.FromCodeAndMessage(
                        readinessDecision.ErrorCode ?? UcliCoreErrorCodes.InternalError,
                        readinessDecision.ErrorMessage!,
                        startupFailure: null);
                }

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    var message = $"Unity oneshot IPC request timed out after {deadline.Timeout.TotalMilliseconds:0} milliseconds.";
                    return await CreateStartupTimeoutFailureAsync(
                            unityProject,
                            processHandle,
                            message,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsStartupRetryable(exception))
            {
                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    var message = $"Unity oneshot IPC request timed out after {deadline.Timeout.TotalMilliseconds:0} milliseconds.";
                    return await CreateStartupTimeoutFailureAsync(
                            unityProject,
                            processHandle,
                            message,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<UnityRequestFailure> CreateStartupTimeoutFailureAsync (
        ResolvedUnityProjectContext unityProject,
        IUnityBatchmodeProcessHandle processHandle,
        string message,
        CancellationToken cancellationToken)
    {
        var classifiedFailure = await TryCreateClassifiedStartupFailureAsync(
                unityProject,
                processHandle,
                message,
                cancellationToken)
            .ConfigureAwait(false);
        if (classifiedFailure is not null)
        {
            return classifiedFailure;
        }

        var startupFailure = StartupFailureDetailFactory.CreateEndpointNotRegisteredFailure(
            message,
            ResolveUnityLogPath(unityProject),
            processHandle.ProcessId,
            processHandle.StartTimeUtc,
            DateTimeOffset.UtcNow);
        return UnityIpcFailureClassifier.FromCodeAndMessage(
            ExecutionErrorCodes.IpcTimeout,
            message,
            startupFailure);
    }

    private async ValueTask<UnityRequestFailure> CreateStartupProcessExitFailureAsync (
        ResolvedUnityProjectContext unityProject,
        IUnityBatchmodeProcessHandle processHandle,
        string message,
        CancellationToken cancellationToken)
    {
        var classifiedFailure = await TryCreateClassifiedStartupFailureAsync(
                unityProject,
                processHandle,
                message,
                cancellationToken)
            .ConfigureAwait(false);
        if (classifiedFailure is not null)
        {
            return classifiedFailure;
        }

        var startupFailure = StartupFailureDetailFactory.CreateProcessExitedFailure(
            message,
            ResolveUnityLogPath(unityProject),
            processHandle.ProcessId,
            processHandle.StartTimeUtc,
            DateTimeOffset.UtcNow);
        return UnityIpcFailureClassifier.FromCodeAndMessage(
            DaemonErrorCodes.DaemonStartProcessExited,
            message,
            startupFailure);
    }

    private async ValueTask<UnityRequestFailure?> TryCreateClassifiedStartupFailureAsync (
        ResolvedUnityProjectContext unityProject,
        IUnityBatchmodeProcessHandle processHandle,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        if (unityLogReader is null)
        {
            return null;
        }

        var logReadResult = await unityLogReader.ReadTailAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!logReadResult.IsSuccess || string.IsNullOrWhiteSpace(logReadResult.Text))
        {
            return null;
        }

        var latestStartupLogText = DaemonStartupFailureLogClassifier.GetLatestStartupLogText(logReadResult.Text);
        if (!DaemonStartupFailureLogClassifier.TryClassifyFailure(
                latestStartupLogText,
                DaemonStartupFailureClassificationContext.Batchmode,
                out var classification))
        {
            return null;
        }

        var message = classification.Message;
        var startupFailure = StartupFailureDetailFactory.CreateClassifiedBatchmodeFailure(
            classification,
            string.IsNullOrWhiteSpace(message) ? fallbackMessage : message,
            ResolveUnityLogPath(unityProject),
            processHandle.ProcessId,
            processHandle.StartTimeUtc,
            DateTimeOffset.UtcNow);
        return UnityIpcFailureClassifier.FromCodeAndMessage(
            DaemonErrorCodes.DaemonStartupBlocked,
            CombineStartupFailureMessages(startupFailure.Diagnosis?.Message, fallbackMessage),
            startupFailure);
    }

    private static string CombineStartupFailureMessages (
        string? primaryMessage,
        string fallbackMessage)
    {
        if (string.IsNullOrWhiteSpace(primaryMessage))
        {
            return fallbackMessage;
        }

        if (string.IsNullOrWhiteSpace(fallbackMessage)
            || string.Equals(primaryMessage, fallbackMessage, StringComparison.Ordinal))
        {
            return primaryMessage;
        }

        return $"{primaryMessage}{Environment.NewLine}{fallbackMessage}";
    }

    /// <summary> Appends a process-termination or post-exit lock-file diagnostic without replacing the primary error code. </summary>
    /// <param name="result"> The primary request result. </param>
    /// <param name="terminationResult"> The observed termination result. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <returns> The original result, or a failure with a termination diagnostic appended. Unconfirmed process cleanup produces a non-recoverable failure. </returns>
    private ValueTask<UnityRequestExecutionResult> AppendPostTerminationDiagnosticAsync (
        UnityRequestExecutionResult result,
        ProcessTerminationResult terminationResult,
        ResolvedUnityProjectContext unityProject)
    {
        if (result.IsSuccess || terminationResult == ProcessTerminationResult.None)
        {
            return ValueTask.FromResult(result);
        }

        if (terminationResult == ProcessTerminationResult.ForceKillFailed)
        {
            return ValueTask.FromResult(AppendNonRecoverableProcessCleanupDiagnostic(
                result,
                ForceKillExitUnconfirmedDiagnostic));
        }

        // NOTE: Post-exit UnityLockfile cleanup is diagnostic only; the IPC failure code and outcome remain unchanged.
        return AppendPostUnityProcessExitLockFileDiagnosticAsync(result, unityProject);
    }

    private static UnityRequestExecutionResult AppendNonRecoverableProcessCleanupDiagnostic (
        UnityRequestExecutionResult result,
        string diagnostic)
    {
        var failure = result.FailureInfo!;
        return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
                failure.Code,
                $"{failure.Message}{Environment.NewLine}{diagnostic}",
                failure.StartupFailure),
            result.LifecycleExecutionStart,
            result.LifecycleActionDispatched);
    }

    private async ValueTask<UnityRequestExecutionResult> AppendPostUnityProcessExitLockFileDiagnosticAsync (
        UnityRequestExecutionResult result,
        ResolvedUnityProjectContext unityProject)
    {
        if (result.IsSuccess)
        {
            return result;
        }

        var failure = result.FailureInfo!;
        var message = await AppendPostUnityProcessExitLockFileDiagnosticAsync(failure.Message, unityProject).ConfigureAwait(false);
        if (string.Equals(message, failure.Message, StringComparison.Ordinal))
        {
            return result;
        }

        return UnityRequestExecutionResult.Failure(
            new UnityRequestFailure(
                failure.FailureKind,
                failure.Code,
                message,
                failure.StartupFailure),
            result.LifecycleExecutionStart,
            result.LifecycleActionDispatched);
    }

    private async ValueTask<string> AppendPostUnityProcessExitLockFileDiagnosticAsync (
        string message,
        ResolvedUnityProjectContext unityProject)
    {
        try
        {
            var preflightResult = await unityProjectLockPreflightService.CleanupStaleLockAfterUnityProcessExitAsync(
                    unityProject,
                    CancellationToken.None)
                .ConfigureAwait(false);
            return UnityProjectLockPreflightErrorFactory.AppendPostExitDiagnostic(message, preflightResult);
        }
        catch (Exception exception)
        {
            return $"{message}{Environment.NewLine}Post-exit Unity project lock cleanup failed. {exception.Message}";
        }
    }

    /// <summary> Returns whether a startup probe exception can be retried before the deadline expires. </summary>
    /// <param name="exception"> The exception observed during the startup probe. Must not be <see langword="null" />. </param>
    /// <returns> <see langword="true" /> for transient connection failures; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    private static bool IsStartupRetryable (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is TimeoutException
            or IpcConnectException
            or IpcResponseReadInterruptedException;
    }

    private static bool IsSessionTokenInvalid (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Errors.Any(
            static error =>
                error.Code == IpcSessionErrorCodes.SessionTokenInvalid);
    }

    /// <summary> Calculates one startup retry delay bounded by the remaining timeout. </summary>
    /// <param name="remainingTimeout"> The remaining timeout budget. </param>
    /// <returns> The retry delay, capped by the remaining timeout. </returns>
    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        if (remainingTimeout < StartupRetryDelay)
        {
            return remainingTimeout;
        }

        return StartupRetryDelay;
    }

    private static TimeSpan GetCleanupAttemptTimeout (TimeSpan remainingTimeout)
    {
        return remainingTimeout < TimeSpan.FromSeconds(1)
            ? remainingTimeout
            : TimeSpan.FromSeconds(1);
    }

    private TimeSpan GetCleanupRetryDelay (TimeSpan remainingTimeout)
    {
        if (remainingTimeout < cleanupPolicy.RetryDelay)
        {
            return remainingTimeout;
        }

        return cleanupPolicy.RetryDelay;
    }

    private static bool IsCleanupShutdownRetryable (Exception exception)
    {
        return exception is TimeoutException or IOException or ObjectDisposedException;
    }

    private static bool IsCleanupShutdownResponseRetryable (IpcError error)
    {
        return error.Code == EditorLifecycleErrorCodes.EditorBusy;
    }

    private static bool ShouldRetryRejectedStart (
        UnityIpcDispatchRequest dispatchRequest,
        OneshotPreparedDispatchOutcome dispatchOutcome)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(dispatchOutcome);
        if (dispatchRequest.StartAdmissionPolicy is null
            || dispatchOutcome.LifecycleExecutionStart is not null
            || dispatchOutcome.Failure is not null
            || dispatchOutcome.Response is null)
        {
            return false;
        }

        var firstError = dispatchOutcome.Response.Errors.FirstOrDefault();
        return firstError is not null
            && dispatchRequest.StartAdmissionPolicy
                .ShouldRetryAfterRejectedStart(firstError.Code);
    }

    private sealed record OneshotReconnectProbeOutcome
    {
        private OneshotReconnectProbeOutcome (
            IpcOneshotBootstrapEnvelope? envelope,
            UnityRequestExecutionResult? result)
        {
            if (envelope is not null && result is not null)
            {
                throw new ArgumentException(
                    "A reconnect probe cannot contain both an owned envelope and a completed result.");
            }

            Envelope = envelope;
            Result = result;
        }

        public IpcOneshotBootstrapEnvelope? Envelope { get; }

        public UnityRequestExecutionResult? Result { get; }

        public static OneshotReconnectProbeOutcome NotOwned ()
        {
            return new OneshotReconnectProbeOutcome(
                envelope: null,
                result: null);
        }

        public static OneshotReconnectProbeOutcome Owned (
            IpcOneshotBootstrapEnvelope envelope)
        {
            return new OneshotReconnectProbeOutcome(
                envelope ?? throw new ArgumentNullException(nameof(envelope)),
                result: null);
        }

        public static OneshotReconnectProbeOutcome Completed (
            UnityRequestExecutionResult result)
        {
            return new OneshotReconnectProbeOutcome(
                envelope: null,
                result ?? throw new ArgumentNullException(nameof(result)));
        }
    }

    private sealed record OneshotPreparedDispatchOutcome
    {
        private OneshotPreparedDispatchOutcome (
            IpcResponse? response,
            UnityRequestFailure? failure,
            LifecycleExecutionStartBinding? lifecycleExecutionStart,
            bool actionDispatched,
            LifecycleExecutionHostExitObservation? confirmedHostExit)
        {
            Response = response;
            Failure = failure;
            LifecycleExecutionStart = lifecycleExecutionStart;
            ActionDispatched = actionDispatched;
            ConfirmedHostExit = confirmedHostExit;
        }

        public IpcResponse? Response { get; }

        public UnityRequestFailure? Failure { get; }

        public LifecycleExecutionStartBinding? LifecycleExecutionStart { get; }

        public bool ActionDispatched { get; }

        public LifecycleExecutionHostExitObservation? ConfirmedHostExit { get; }

        public static OneshotPreparedDispatchOutcome Responded (
            IpcResponse response,
            LifecycleExecutionStartBinding? lifecycleExecutionStart,
            bool actionDispatched)
        {
            return new OneshotPreparedDispatchOutcome(
                response ?? throw new ArgumentNullException(nameof(response)),
                failure: null,
                lifecycleExecutionStart,
                actionDispatched,
                confirmedHostExit: null);
        }

        public static OneshotPreparedDispatchOutcome Failed (
            UnityRequestFailure failure,
            LifecycleExecutionStartBinding? lifecycleExecutionStart,
            bool actionDispatched = false,
            LifecycleExecutionHostExitObservation? confirmedHostExit = null)
        {
            return new OneshotPreparedDispatchOutcome(
                response: null,
                failure ?? throw new ArgumentNullException(nameof(failure)),
                lifecycleExecutionStart,
                actionDispatched,
                confirmedHostExit);
        }
    }

    private static UnityRequestExecutionResult CreateConfirmedHostExitResult (
        LifecycleExecutionStartBinding requiredStart,
        bool lifecycleActionDispatched)
    {
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                EditorLifecycleErrorCodes.EditorUnavailable,
                "The Unity Editor process that owns the Lifecycle Execution exited during reconnect."),
            requiredStart,
            lifecycleActionDispatched,
            new LifecycleExecutionHostExitObservation(
                requiredStart.Host.Process));
    }

    private bool TryGetDispatchBudget (
        ExecutionDeadline deadline,
        out TimeSpan remainingTimeout,
        out int remainingMilliseconds,
        out DateTimeOffset requestDeadlineUtc)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        requestDeadlineUtc = deadline.UtcDeadline;
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            remainingMilliseconds = 0;
            return false;
        }

        var utcRemaining = requestDeadlineUtc - timeProvider.GetUtcNow();
        if (utcRemaining <= TimeSpan.Zero)
        {
            remainingTimeout = TimeSpan.Zero;
            remainingMilliseconds = 0;
            return false;
        }

        if (utcRemaining < remainingTimeout)
        {
            remainingTimeout = utcRemaining;
        }

        var roundedMilliseconds = Math.Ceiling(remainingTimeout.TotalMilliseconds);
        remainingMilliseconds = roundedMilliseconds >= int.MaxValue
            ? int.MaxValue
            : (int)roundedMilliseconds;
        return remainingMilliseconds > 0;
    }

    /// <summary> Resolves whether the dispatch payload requests fail-fast readiness behavior. </summary>
    /// <param name="dispatchRequest"> The dispatch request to inspect. Must not be <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when a known request payload requests fail-fast readiness; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="dispatchRequest" /> is <see langword="null" />. </exception>
    private static bool ResolveStartupProbeFailFast (UnityIpcDispatchRequest dispatchRequest)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        if (dispatchRequest.StartAdmissionPolicy is not null)
        {
            return dispatchRequest.StartAdmissionPolicy.FailFast;
        }

        return dispatchRequest.Method switch
        {
            UnityIpcMethod.Execute => TryReadFailFast<IpcExecuteRequest>(dispatchRequest.Payload, static request => request.FailFast),
            UnityIpcMethod.TestRun => TryReadFailFast<IpcTestRunRequest>(dispatchRequest.Payload, static request => request.FailFast),
            UnityIpcMethod.OpsRead => TryReadFailFast<IpcOpsReadRequest>(
                dispatchRequest.Payload,
                static request => request.RequireReadinessGate && request.FailFast),
            UnityIpcMethod.IndexAssetsRead => TryReadFailFast<IpcIndexAssetsReadRequest>(dispatchRequest.Payload, static request => request.FailFast),
            UnityIpcMethod.IndexSceneTreeLiteRead => TryReadFailFast<IpcIndexSceneTreeLiteReadRequest>(dispatchRequest.Payload, static request => request.FailFast),
            UnityIpcMethod.Ping => TryReadFailFast<IpcPingRequest>(dispatchRequest.Payload, static request => request.FailFast),
            _ => false,
        };
    }

    private static bool IsStartupLifecycleDispatchAllowed (
        UnityIpcDispatchRequest dispatchRequest,
        UnityEditorObservation pingResponse)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(pingResponse);

        return UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            dispatchRequest.Method,
            pingResponse.State.LifecycleState);
    }

    private static bool TryReadFailFast<TRequest> (
        JsonElement payload,
        Func<TRequest, bool> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return IpcPayloadCodec.TryDeserialize(payload, out TRequest request, out _)
            && selector(request);
    }

    /// <summary> Creates the startup probe request for one session token. </summary>
    /// <param name="sessionToken"> The canonical session token assigned to the launched oneshot process. </param>
    /// <param name="requestId"> The non-empty identifier reused by every startup-probe attempt. </param>
    /// <param name="requestDeadlineUtc"> The UTC deadline shared by every startup-probe attempt. </param>
    /// <returns> The IPC ping request used to verify startup readiness. </returns>
    private static IpcRequestEnvelope CreateStartupProbeRequest (
        IpcSessionToken sessionToken,
        Guid requestId,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.OneshotStartup));
        return UnityIpcRequestFactory.Create(
            sessionToken,
            UnityIpcMethod.Ping,
            payload,
            requestId,
            IpcResponseMode.Single,
            requestDeadlineUtc,
            requestDeadlineRemainingMilliseconds);
    }

    /// <summary> Creates the shutdown request shared by cleanup attempts for one launched process. </summary>
    /// <param name="sessionToken"> The canonical session token assigned to the launched oneshot process. </param>
    /// <param name="requestId"> The non-empty identifier reused by every shutdown attempt. </param>
    /// <param name="requestDeadlineUtc"> The UTC deadline shared by every shutdown attempt. </param>
    /// <returns> The IPC shutdown request used during process cleanup. </returns>
    private static IpcRequestEnvelope CreateShutdownRequest (
        IpcSessionToken sessionToken,
        Guid requestId,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest(CleanupShutdownRequestedBy));
        return UnityIpcRequestFactory.Create(
            sessionToken,
            UnityIpcMethod.Shutdown,
            payload,
            requestId,
            IpcResponseMode.Single,
            requestDeadlineUtc,
            requestDeadlineRemainingMilliseconds);
    }

    private static AbsolutePath ResolveUnityLogPath (ResolvedUnityProjectContext unityProject)
    {
        return UcliStoragePathResolver.ResolveUnityLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
    }

    /// <summary> Waits for the launched oneshot Unity process to exit after response handling completes. </summary>
    /// <param name="processHandle"> The launched process handle. </param>
    /// <param name="timeout"> The maximum wait time. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> <see langword="null" /> when the process exits with code zero or without an exit code; otherwise the exit failure. </returns>
    private static async ValueTask<ExecutionError?> WaitForExitAsync (
        IUnityBatchmodeProcessHandle processHandle,
        TimeSpan timeout,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout, timeProvider);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationTokenSource.Token);

        try
        {
            await processHandle.WaitForExitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            return ExecutionError.Timeout(
                $"Unity oneshot process did not exit within {timeout.TotalMilliseconds:0} milliseconds after response handling completed.");
        }

        if (processHandle.ExitCode is int exitCode && exitCode != 0)
        {
            return ExecutionError.InternalError(
                $"Unity oneshot process exited with code {exitCode}.");
        }

        return null;
    }

    /// <summary> Releases one owned resource without allowing release failure to replace the primary outcome. </summary>
    private sealed class BestEffortAsyncDisposable : IAsyncDisposable
    {
        private IAsyncDisposable? disposable;

        /// <summary> Initializes a new instance of the <see cref="BestEffortAsyncDisposable" /> class. </summary>
        /// <param name="disposable"> The owned resource to release. </param>
        public BestEffortAsyncDisposable (IAsyncDisposable disposable)
        {
            this.disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
        }

        /// <summary> Transfers resource release responsibility to another owner. </summary>
        public void RelinquishOwnership ()
        {
            disposable = null;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync ()
        {
            var ownedDisposable = disposable;
            disposable = null;
            if (ownedDisposable is null)
            {
                return;
            }

            try
            {
                await ownedDisposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A completed response, primary failure, cancellation, or progress callback exception remains authoritative.
            }
        }
    }
}
