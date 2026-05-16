using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Executes one IPC request through Unity oneshot batchmode startup and shared IPC transport. </summary>
internal sealed class UnityOneshotIpcClient : IUnityIpcClient
{
    private const string CleanupShutdownRequestedBy = "ucli-oneshot-cleanup";

    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan DefaultCleanupTimeout = TimeSpan.FromSeconds(30);

    private static readonly ProcessTerminationPolicy EmergencyTerminationPolicy = new(
        ProcessTerminationMode.GracefulThenKill,
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(10));

    private readonly IUnityBatchmodeProcessLauncher batchmodeProcessLauncher;

    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IUnityIpcTransportClient transportClient;

    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    private readonly IUnityLogReader? unityLogReader;

    private readonly TimeSpan cleanupTimeout;

    private readonly TimeSpan cleanupRetryDelay;

    /// <summary> Initializes a new instance of the <see cref="UnityOneshotIpcClient" /> class. </summary>
    /// <param name="batchmodeProcessLauncher"> The Unity batchmode process launcher dependency. </param>
    /// <param name="endpointResolver"> The IPC endpoint resolver dependency. </param>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="unityProjectLockPreflightService"> The Unity project lock preflight service dependency. </param>
    public UnityOneshotIpcClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IIpcEndpointResolver endpointResolver,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        IUnityLogReader? unityLogReader = null)
        : this(
            batchmodeProcessLauncher,
            endpointResolver,
            transportClient,
            lifecycleLockProvider,
            unityProjectLockPreflightService,
            unityLogReader,
            DefaultCleanupTimeout,
            StartupRetryDelay)
    {
    }

    internal UnityOneshotIpcClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IIpcEndpointResolver endpointResolver,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        TimeSpan cleanupTimeout,
        TimeSpan cleanupRetryDelay)
        : this(
            batchmodeProcessLauncher,
            endpointResolver,
            transportClient,
            lifecycleLockProvider,
            unityProjectLockPreflightService,
            unityLogReader: null,
            cleanupTimeout,
            cleanupRetryDelay)
    {
    }

    internal UnityOneshotIpcClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IIpcEndpointResolver endpointResolver,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        IUnityLogReader? unityLogReader,
        TimeSpan cleanupTimeout,
        TimeSpan cleanupRetryDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cleanupTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cleanupRetryDelay, TimeSpan.Zero);

        this.batchmodeProcessLauncher = batchmodeProcessLauncher ?? throw new ArgumentNullException(nameof(batchmodeProcessLauncher));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.unityProjectLockPreflightService = unityProjectLockPreflightService ?? throw new ArgumentNullException(nameof(unityProjectLockPreflightService));
        this.unityLogReader = unityLogReader;
        this.cleanupTimeout = cleanupTimeout;
        this.cleanupRetryDelay = cleanupRetryDelay;
    }

    /// <inheritdoc />
    public UnityExecutionTarget Target => UnityExecutionTarget.Oneshot;

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProject.UnityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProject.RepositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProject.ProjectFingerprint);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var deadline = ExecutionDeadline.Start(timeout);
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var endpoint = endpointResolver.Resolve(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        try
        {
            if (!deadline.TryGetRemainingTimeout(out var lockTimeout))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.OneshotTimeout(timeout));
            }

            await using var lifecycleLock = await lifecycleLockProvider.AcquireAsync(
                    new ProjectLifecycleLockRequest(unityProject.UnityProjectRoot),
                    lockTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            var unityLogDirectoryPath = Path.GetDirectoryName(unityLogPath);
            if (!string.IsNullOrWhiteSpace(unityLogDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(unityLogDirectoryPath);
            }

            if (!deadline.TryGetRemainingTimeout(out var launchRemainingTimeout))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.OneshotTimeout(timeout));
            }

            var sessionToken = CreateSessionToken();
            var launchResult = await batchmodeProcessLauncher.LaunchAsync(
                    unityProject,
                    new IpcOneshotBootstrapArguments(
                        ParentProcessId: Environment.ProcessId,
                        ProjectFingerprint: unityProject.ProjectFingerprint,
                        SessionToken: sessionToken,
                        ExitDeadlineUtc: DateTimeOffset.UtcNow + launchRemainingTimeout,
                        EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
                        EndpointAddress: endpoint.Address),
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromExecutionError(launchResult.Error!));
            }

            await using var processHandle = launchResult.ProcessHandle!;
            var shouldTerminateProcess = true;
            var terminationResult = ProcessTerminationResult.None;
            UnityRequestExecutionResult result;
            try
            {
                var startupProbeFailure = await WaitUntilReachableAsync(
                        unityProject,
                        sessionToken,
                        ResolveStartupProbeFailFast(dispatchRequest),
                        deadline,
                        processHandle,
                        timeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupProbeFailure != null)
                {
                    result = UnityRequestExecutionResult.Failure(startupProbeFailure);
                }
                else if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
                {
                    result = UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.OneshotTimeout(timeout));
                }
                else
                {
                    var response = await transportClient.SendAsync(
                            unityProject.RepositoryRoot,
                            unityProject.ProjectFingerprint,
                            UnityIpcRequestFactory.Create(
                                sessionToken,
                                dispatchRequest.Method,
                                dispatchRequest.Payload),
                            requestTimeout,
                            cancellationToken)
                        .ConfigureAwait(false);
                    var exitWaitError = await WaitForExitAsync(processHandle, cleanupTimeout, cancellationToken).ConfigureAwait(false);
                    if (exitWaitError != null)
                    {
                        result = UnityRequestExecutionResult.Failure(
                            UnityIpcFailureClassifier.FromExecutionError(exitWaitError));
                    }
                    else
                    {
                        shouldTerminateProcess = false;
                        result = UnityRequestExecutionResult.Success(UnityRequestResponseFactory.Create(response));
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result = UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromOneshotDispatchException(exception, timeout));
            }
            finally
            {
                if (shouldTerminateProcess && !processHandle.HasExited)
                {
                    terminationResult = await CleanupLaunchedProcessAsync(
                            unityProject,
                            sessionToken,
                            processHandle)
                        .ConfigureAwait(false);
                }
            }

            return await AppendPostTerminationLockFileDiagnosticAsync(result, terminationResult, unityProject).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.FromOneshotDispatchException(exception, timeout));
        }
    }

    private async ValueTask<ProcessTerminationResult> CleanupLaunchedProcessAsync (
        ResolvedUnityProjectContext unityProject,
        string sessionToken,
        IUnityBatchmodeProcessHandle processHandle)
    {
        if (processHandle.HasExited)
        {
            return ProcessTerminationResult.None;
        }

        var cleanupDeadline = ExecutionDeadline.Start(cleanupTimeout);
        if (await TryRequestShutdownUntilCleanupDeadlineAsync(unityProject, sessionToken, processHandle, cleanupDeadline).ConfigureAwait(false)
            && !processHandle.HasExited
            && cleanupDeadline.TryGetRemainingTimeout(out var exitTimeout))
        {
            var exitWaitError = await WaitForExitAsync(processHandle, exitTimeout, CancellationToken.None).ConfigureAwait(false);
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
        string sessionToken,
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
                var response = await transportClient.SendAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        CreateShutdownRequest(sessionToken),
                        GetCleanupAttemptTimeout(remainingTimeout),
                        CancellationToken.None)
                    .ConfigureAwait(false);
                return !IpcResponseFailureReader.TryRead(response, out _, out _);
            }
            catch (Exception exception) when (IsCleanupShutdownRetryable(exception))
            {
                if (!cleanupDeadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return false;
                }

                await Task.Delay(GetCleanupRetryDelay(remainingTimeout), CancellationToken.None).ConfigureAwait(false);
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
    /// <param name="sessionToken"> The session token assigned to the launched oneshot process. Must not be null or white-space. </param>
    /// <param name="failFast"> Whether readiness probing should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="deadline"> The shared request deadline. </param>
    /// <param name="processHandle"> The launched process handle. </param>
    /// <param name="timeout"> The original request timeout used for diagnostics. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> <see langword="null" /> when startup is reachable; otherwise the startup failure. </returns>
    private async ValueTask<UnityRequestFailure?> WaitUntilReachableAsync (
        ResolvedUnityProjectContext unityProject,
        string sessionToken,
        bool failFast,
        ExecutionDeadline deadline,
        IUnityBatchmodeProcessHandle processHandle,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                var message = $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.";
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
                var pingResponse = await transportClient.SendAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        CreateStartupProbeRequest(sessionToken),
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

                var readinessDecision = UnityDaemonReadinessPolicy.Evaluate(payload!, failFast);
                if (readinessDecision.IsReady)
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
                    var message = $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.";
                    return await CreateStartupTimeoutFailureAsync(
                            unityProject,
                            processHandle,
                            message,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await Task.Delay(GetRetryDelay(remainingTimeout), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsStartupRetryable(exception))
            {
                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    var message = $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.";
                    return await CreateStartupTimeoutFailureAsync(
                            unityProject,
                            processHandle,
                            message,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await Task.Delay(GetRetryDelay(remainingTimeout), cancellationToken).ConfigureAwait(false);
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

        var message = classification!.Message;
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

    /// <summary> Appends a post-exit Unity lock-file cleanup diagnostic after uCLI has terminated a oneshot Unity process. </summary>
    /// <param name="result"> The primary request result. Must be a failure when <paramref name="terminationResult" /> is not <see cref="ProcessTerminationResult.None" />. </param>
    /// <param name="terminationResult"> The observed termination result. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <returns> The original result, or an equivalent failure with a post-exit cleanup diagnostic appended. </returns>
    private ValueTask<UnityRequestExecutionResult> AppendPostTerminationLockFileDiagnosticAsync (
        UnityRequestExecutionResult result,
        ProcessTerminationResult terminationResult,
        ResolvedUnityProjectContext unityProject)
    {
        if (result.IsSuccess || terminationResult == ProcessTerminationResult.None)
        {
            return ValueTask.FromResult(result);
        }

        // NOTE: Post-exit UnityLockfile cleanup is diagnostic only; the IPC failure code and outcome remain unchanged.
        return AppendPostUnityProcessExitLockFileDiagnosticAsync(result, unityProject);
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
        return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
            failure.Code,
            message,
            failure.StartupFailure));
    }

    private async ValueTask<string> AppendPostUnityProcessExitLockFileDiagnosticAsync (
        string message,
        ResolvedUnityProjectContext unityProject)
    {
        var preflightResult = await unityProjectLockPreflightService.CleanupStaleLockAfterUnityProcessExitAsync(
                unityProject,
                CancellationToken.None)
            .ConfigureAwait(false);
        return UnityProjectLockPreflightErrorFactory.AppendPostExitDiagnostic(message, preflightResult);
    }

    /// <summary> Returns whether a startup probe exception can be retried before the deadline expires. </summary>
    /// <param name="exception"> The exception observed during the startup probe. Must not be <see langword="null" />. </param>
    /// <returns> <see langword="true" /> for transient connection failures; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    private static bool IsStartupRetryable (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is TimeoutException or System.Net.Sockets.SocketException;
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
        if (remainingTimeout < cleanupRetryDelay)
        {
            return remainingTimeout;
        }

        return cleanupRetryDelay;
    }

    private static bool IsCleanupShutdownRetryable (Exception exception)
    {
        return exception is TimeoutException or System.Net.Sockets.SocketException or IOException or ObjectDisposedException;
    }

    /// <summary> Resolves whether the dispatch payload requests fail-fast readiness behavior. </summary>
    /// <param name="dispatchRequest"> The dispatch request to inspect. Must not be <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when a known request payload requests fail-fast readiness; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="dispatchRequest" /> is <see langword="null" />. </exception>
    private static bool ResolveStartupProbeFailFast (UnityIpcDispatchRequest dispatchRequest)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);

        return dispatchRequest.Method switch
        {
            IpcMethodNames.Execute => TryReadFailFast<IpcExecuteRequest>(dispatchRequest.Payload, static request => request.FailFast),
            IpcMethodNames.TestRun => TryReadFailFast<IpcTestRunRequest>(dispatchRequest.Payload, static request => request.FailFast),
            IpcMethodNames.OpsRead => TryReadFailFast<IpcOpsReadRequest>(
                dispatchRequest.Payload,
                static request => request.RequireReadinessGate && request.FailFast),
            IpcMethodNames.IndexAssetsRead => TryReadFailFast<IpcIndexAssetsReadRequest>(dispatchRequest.Payload, static request => request.FailFast),
            IpcMethodNames.IndexSceneTreeLiteRead => TryReadFailFast<IpcIndexSceneTreeLiteReadRequest>(dispatchRequest.Payload, static request => request.FailFast),
            IpcMethodNames.Ping => TryReadFailFast<IpcPingRequest>(dispatchRequest.Payload, static request => request.FailFast),
            _ => false,
        };
    }

    private static bool TryReadFailFast<TRequest> (
        JsonElement payload,
        Func<TRequest, bool> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return IpcPayloadCodec.TryDeserialize(payload, out TRequest request, out _)
            && selector(request);
    }

    /// <summary> Creates one opaque session token for correlating the launched oneshot Unity process. </summary>
    /// <returns> A non-empty lowercase hexadecimal token without separators. </returns>
    private static string CreateSessionToken ()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary> Creates the startup probe request for one session token. </summary>
    /// <param name="sessionToken"> The session token assigned to the launched oneshot process. Must not be null or white-space. </param>
    /// <returns> The IPC ping request used to verify startup readiness. </returns>
    private static IpcRequest CreateStartupProbeRequest (string sessionToken)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.OneshotStartup));
        return UnityIpcRequestFactory.Create(sessionToken, IpcMethodNames.Ping, payload);
    }

    private static IpcRequest CreateShutdownRequest (string sessionToken)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest(CleanupShutdownRequestedBy));
        return UnityIpcRequestFactory.Create(sessionToken, IpcMethodNames.Shutdown, payload);
    }

    private static string ResolveUnityLogPath (ResolvedUnityProjectContext unityProject)
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
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);

        try
        {
            await processHandle.WaitForExitAsync(timeoutCancellationTokenSource.Token).ConfigureAwait(false);
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
}
