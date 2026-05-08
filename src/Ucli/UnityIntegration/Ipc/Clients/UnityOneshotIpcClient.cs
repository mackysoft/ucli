using System.Text.Json;
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
    private const string StartupProbeClientVersion = "ucli-oneshot-startup";
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

    private readonly IUnityProjectLockFileProbe unityProjectLockFileProbe;

    private readonly TimeSpan cleanupTimeout;

    private readonly TimeSpan cleanupRetryDelay;

    /// <summary> Initializes a new instance of the <see cref="UnityOneshotIpcClient" /> class. </summary>
    /// <param name="batchmodeProcessLauncher"> The Unity batchmode process launcher dependency. </param>
    /// <param name="endpointResolver"> The IPC endpoint resolver dependency. </param>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="unityProjectLockFileProbe"> The Unity project lock-file probe dependency. </param>
    public UnityOneshotIpcClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IIpcEndpointResolver endpointResolver,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockFileProbe unityProjectLockFileProbe)
        : this(
            batchmodeProcessLauncher,
            endpointResolver,
            transportClient,
            lifecycleLockProvider,
            unityProjectLockFileProbe,
            DefaultCleanupTimeout,
            StartupRetryDelay)
    {
    }

    internal UnityOneshotIpcClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IIpcEndpointResolver endpointResolver,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockFileProbe unityProjectLockFileProbe,
        TimeSpan cleanupTimeout,
        TimeSpan cleanupRetryDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cleanupTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cleanupRetryDelay, TimeSpan.Zero);

        this.batchmodeProcessLauncher = batchmodeProcessLauncher ?? throw new ArgumentNullException(nameof(batchmodeProcessLauncher));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.unityProjectLockFileProbe = unityProjectLockFileProbe ?? throw new ArgumentNullException(nameof(unityProjectLockFileProbe));
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

            await using var lifecycleLock = await lifecycleLockProvider.Acquire(
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
            var launchResult = await batchmodeProcessLauncher.Launch(
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
                var startupProbeError = await WaitUntilReachableAsync(
                        unityProject,
                        sessionToken,
                        ResolveStartupProbeFailFast(dispatchRequest),
                        deadline,
                        processHandle,
                        timeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupProbeError != null)
                {
                    result = UnityRequestExecutionResult.Failure(
                        UnityIpcFailureClassifier.FromExecutionError(startupProbeError));
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
                    terminationResult = await CleanupLaunchedProcess(
                            unityProject,
                            sessionToken,
                            processHandle)
                        .ConfigureAwait(false);
                }
            }

            return AppendPostTerminationLockFileDiagnostic(result, terminationResult, unityProject.UnityProjectRoot);
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

    private async ValueTask<ProcessTerminationResult> CleanupLaunchedProcess (
        ResolvedUnityProjectContext unityProject,
        string sessionToken,
        IUnityBatchmodeProcessHandle processHandle)
    {
        if (processHandle.HasExited)
        {
            return ProcessTerminationResult.None;
        }

        var cleanupDeadline = ExecutionDeadline.Start(cleanupTimeout);
        if (await TryRequestShutdownUntilCleanupDeadline(unityProject, sessionToken, processHandle, cleanupDeadline).ConfigureAwait(false)
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

    private async ValueTask<bool> TryRequestShutdownUntilCleanupDeadline (
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
    private async ValueTask<ExecutionError?> WaitUntilReachableAsync (
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
                return ExecutionError.Timeout(
                    $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
            }

            if (processHandle.HasExited)
            {
                var projectAlreadyOpenError = TryCreateProjectAlreadyOpenErrorFromUnityLock(unityProject.UnityProjectRoot);
                if (projectAlreadyOpenError != null)
                {
                    return projectAlreadyOpenError;
                }

                var exitCode = processHandle.ExitCode;
                return ExecutionError.InternalError(
                    exitCode is int code
                        ? $"Unity oneshot process exited before startup readiness was confirmed. ExitCode={code}."
                        : "Unity oneshot process exited before startup readiness was confirmed.");
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
                    return ExecutionError.InternalError(
                        $"Unity oneshot startup probe returned an invalid response. {error!.Message}");
                }

                var readinessDecision = UnityDaemonReadinessPolicy.Evaluate(payload, failFast);
                if (readinessDecision.IsReady)
                {
                    return null;
                }

                if (readinessDecision.IsFailure)
                {
                    return ExecutionError.InternalError(
                        readinessDecision.ErrorMessage!,
                        readinessDecision.ErrorCode);
                }

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return ExecutionError.Timeout(
                        $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
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
                    return ExecutionError.Timeout(
                        $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
                }

                await Task.Delay(GetRetryDelay(remainingTimeout), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary> Creates a project-open error when Unity reports the project-local lock file as locked. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. Must not be null or white-space. </param>
    /// <returns> A classified project-open error when Unity owns the lock file; otherwise <see langword="null" />. </returns>
    private ExecutionError? TryCreateProjectAlreadyOpenErrorFromUnityLock (string unityProjectRoot)
    {
        var lockFileProbeResult = unityProjectLockFileProbe.Probe(unityProjectRoot);
        if (!lockFileProbeResult.IsSuccess)
        {
            return ExecutionError.InternalError(
                lockFileProbeResult.ErrorMessage!,
                UcliCoreErrorCodes.InternalError);
        }

        if (!lockFileProbeResult.IsLocked)
        {
            return null;
        }

        return ExecutionError.InternalError(
            UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProjectRoot, lockFileProbeResult.LockFilePath),
            UnityProcessErrorCodes.UnityProjectAlreadyOpen);
    }

    /// <summary> Appends a residual Unity lock-file diagnostic after uCLI has terminated a oneshot Unity process. </summary>
    /// <param name="result"> The primary request result. Must be a failure when <paramref name="terminationResult" /> is not <see cref="ProcessTerminationResult.None" />. </param>
    /// <param name="terminationResult"> The observed termination result. </param>
    /// <param name="unityProjectRoot"> The Unity project root path. Must not be null or white-space. </param>
    /// <returns> The original result, or an equivalent failure with a residual-lock diagnostic appended. </returns>
    private UnityRequestExecutionResult AppendPostTerminationLockFileDiagnostic (
        UnityRequestExecutionResult result,
        ProcessTerminationResult terminationResult,
        string unityProjectRoot)
    {
        if (result.IsSuccess || terminationResult == ProcessTerminationResult.None)
        {
            return result;
        }

        // NOTE: Residual UnityLockfile is diagnostic only; the IPC failure code and outcome remain unchanged.
        var lockFileProbeResult = unityProjectLockFileProbe.Probe(unityProjectRoot);
        if (!lockFileProbeResult.IsSuccess || !lockFileProbeResult.IsLocked)
        {
            return result;
        }

        var failure = result.FailureInfo!;
        return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
            failure.Code,
            $"{failure.Message} {UnityProjectLockFailureMessage.CreateTerminatedProcessLockFileRemains(unityProjectRoot, lockFileProbeResult.LockFilePath!)}"));
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
        var payload = IpcPayloadCodec.SerializeToElement(new IpcPingRequest(StartupProbeClientVersion));
        return UnityIpcRequestFactory.Create(sessionToken, IpcMethodNames.Ping, payload);
    }

    private static IpcRequest CreateShutdownRequest (string sessionToken)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest(CleanupShutdownRequestedBy));
        return UnityIpcRequestFactory.Create(sessionToken, IpcMethodNames.Shutdown, payload);
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
