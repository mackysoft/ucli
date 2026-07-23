using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Validates and dispatches supervisor IPC requests to the appropriate lifecycle coordinator. </summary>
internal sealed class SupervisorRequestDispatcher
{
    private readonly SupervisorActivityTracker activityTracker;

    private readonly SupervisorProjectCoordinator projectCoordinator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorRequestDispatcher" /> class. </summary>
    /// <param name="activityTracker"> The supervisor activity-tracker dependency. </param>
    /// <param name="projectCoordinator"> The supervisor project-coordinator dependency. </param>
    /// <param name="timeProvider"> The time provider used for frame deadline accounting. </param>
    public SupervisorRequestDispatcher (
        SupervisorActivityTracker activityTracker,
        SupervisorProjectCoordinator projectCoordinator,
        TimeProvider timeProvider)
    {
        this.activityTracker = activityTracker ?? throw new ArgumentNullException(nameof(activityTracker));
        this.projectCoordinator = projectCoordinator ?? throw new ArgumentNullException(nameof(projectCoordinator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Handles one supervisor IPC connection. </summary>
    /// <param name="stream"> The accepted transport stream. </param>
    /// <param name="runtimeContext"> The immutable supervisor runtime context. </param>
    /// <param name="initialFrameReadTimeout"> The upper bound for receiving the first request frame. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the listener. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="initialFrameReadTimeout" /> is not positive. </exception>
    public async Task HandleConnectionAsync (
        Stream stream,
        SupervisorRuntimeContext runtimeContext,
        TimeSpan initialFrameReadTimeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(runtimeContext);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialFrameReadTimeout, TimeSpan.Zero);

        activityTracker.Touch();

        var frameReadTask = Task.Run(
            () => IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken)
                .AsTask(),
            CancellationToken.None);
        using var frameReadTimeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutTask = Task.Delay(
            initialFrameReadTimeout,
            timeProvider,
            frameReadTimeoutCancellationTokenSource.Token);
        var completedTask = await Task.WhenAny(frameReadTask, timeoutTask).ConfigureAwait(false);
        if (!ReferenceEquals(completedTask, frameReadTask))
        {
            ObserveFault(frameReadTask);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        frameReadTimeoutCancellationTokenSource.Cancel();
        var readResult = await frameReadTask.ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            var malformedResponse = SupervisorIpcResponseFactory.CreateMalformedFrameResponse(
                readResult.ErrorKind,
                readResult.ErrorMessage);
            await TryWriteResponseAsync(stream, malformedResponse, cancellationToken).ConfigureAwait(false);
            return;
        }

        var requestEnvelope = readResult.Value;
        var requestDeadline = StartRequestDeadline(requestEnvelope);
        using var requestScope = activityTracker.BeginRequest();
        var validationResult = ValidateRequest(requestEnvelope, runtimeContext);
        if (!validationResult.IsSuccess)
        {
            var validationErrorResponse = validationResult.ErrorResponse!;
            if (validationResult.ResponseMode == IpcResponseMode.Stream)
            {
                var validationStreamCompletionDeadline = requestDeadline is null
                    ? ExecutionDeadline.Start(SupervisorConstants.EnsureRunningTerminalResponseGrace, timeProvider)
                    : requestDeadline.CreateCompletionDeadline(SupervisorConstants.EnsureRunningTerminalResponseGrace);
                var validationCompletionRemaining = TimeSpan.Zero;
                _ = validationStreamCompletionDeadline.TryGetRemainingTimeout(out validationCompletionRemaining);
                using var validationWriteCutoffCancellationTokenSource = new CancellationTokenSource(
                    validationCompletionRemaining,
                    timeProvider);
                using var validationErrorWriter = new IpcStreamFrameWriter(
                    stream,
                    requestEnvelope,
                    cancellationToken,
                    cancellationToken,
                    validationWriteCutoffCancellationTokenSource.Token,
                    writeFailureHandler: null);
                await TryWriteTerminalAsync(
                        validationErrorWriter,
                        validationErrorResponse,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await TryWriteResponseAsync(stream, validationErrorResponse, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var request = validationResult.Request!;
        if (request.ResponseMode == IpcResponseMode.Stream)
        {
            var streamCompletionDeadline = requestDeadline is null
                ? ExecutionDeadline.Start(SupervisorConstants.EnsureRunningTerminalResponseGrace, timeProvider)
                : requestDeadline.CreateCompletionDeadline(SupervisorConstants.EnsureRunningTerminalResponseGrace);
            await HandleStreamingRequestAsync(
                    stream,
                    runtimeContext,
                    request,
                    requestDeadline,
                    streamCompletionDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var response = await ProcessRequestAsync(
                stream,
                runtimeContext,
                request,
                requestDeadline,
                streamWriter: null,
                requestLifetimeStarted: null,
                cancellationToken)
            .ConfigureAwait(false);
        await TryWriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IpcResponse> ProcessRequestAsync (
        Stream stream,
        SupervisorRuntimeContext runtimeContext,
        ValidatedSupervisorIpcRequest request,
        ExecutionDeadline? requestDeadline,
        IpcStreamFrameWriter? streamWriter,
        Action<SupervisorRequestLifetime>? requestLifetimeStarted,
        CancellationToken cancellationToken)
    {
        if (streamWriter is not null
            && request.Method != SupervisorIpcMethod.EnsureRunning)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                $"Supervisor IPC responseMode 'stream' is only supported for {ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning)}.");
        }

        if (requestDeadline is null
            || !requestDeadline.TryGetRemainingTimeout(out _))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                ExecutionErrorCodes.IpcTimeout,
                "Supervisor request deadline expired before dispatch.");
        }

        return request.Method switch
        {
            SupervisorIpcMethod.Ping => HandlePing(request, runtimeContext),
            SupervisorIpcMethod.EnsureRunning => await HandleEnsureRunningAsync(
                    stream,
                    request,
                    requestDeadline,
                    runtimeContext,
                    streamWriter,
                    requestLifetimeStarted,
                    cancellationToken)
                .ConfigureAwait(false),
            SupervisorIpcMethod.StopProject => await HandleStopProjectAsync(
                    stream,
                    request,
                    requestDeadline,
                    runtimeContext,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException("Validated supervisor IPC method is not dispatchable."),
        };
    }

    private async ValueTask HandleStreamingRequestAsync (
        Stream stream,
        SupervisorRuntimeContext runtimeContext,
        ValidatedSupervisorIpcRequest request,
        ExecutionDeadline? requestDeadline,
        ExecutionDeadline streamCompletionDeadline,
        CancellationToken cancellationToken)
    {
        SupervisorRequestLifetime? activeRequestLifetime = null;
        var completionRemaining = TimeSpan.Zero;
        _ = streamCompletionDeadline.TryGetRemainingTimeout(out completionRemaining);
        using var writeCutoffCancellationTokenSource = new CancellationTokenSource(
            completionRemaining,
            timeProvider);
        using var streamWriter = new IpcStreamFrameWriter(
            stream,
            request,
            cancellationToken,
            cancellationToken,
            writeCutoffCancellationTokenSource.Token,
            _ => activeRequestLifetime?.CancelForResponseStreamFailure());
        var response = await ProcessRequestAsync(
                stream,
                runtimeContext,
                request,
                requestDeadline,
                streamWriter,
                requestLifetime => activeRequestLifetime = requestLifetime,
                cancellationToken)
            .ConfigureAwait(false);
        await TryWriteTerminalAsync(streamWriter, response, cancellationToken).ConfigureAwait(false);
    }

    private static IpcResponse HandlePing (
        ValidatedSupervisorIpcRequest request,
        SupervisorRuntimeContext runtimeContext)
    {
        return SupervisorIpcResponseFactory.CreateSuccessResponse(
            request,
            new SupervisorIpcContracts.PingResponse(
                Environment.ProcessId,
                runtimeContext.Manifest.IssuedAtUtc));
    }

    private async ValueTask<IpcResponse> HandleEnsureRunningAsync (
        Stream stream,
        ValidatedSupervisorIpcRequest request,
        ExecutionDeadline requestDeadline,
        SupervisorRuntimeContext runtimeContext,
        IpcStreamFrameWriter? streamWriter,
        Action<SupervisorRequestLifetime>? requestLifetimeStarted,
        CancellationToken cancellationToken)
    {
        if (!IpcPayloadCodec.TryDeserialize(
                request.Payload,
                out SupervisorIpcContracts.EnsureRunningRequest payload,
                out var payloadError))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                $"Supervisor ensureRunning payload is invalid. {payloadError.Message}");
        }

        var projectContextResult = TryCreateProjectContext(
            runtimeContext,
            payload.UnityProjectRoot,
            payload.ProjectFingerprint);
        if (!projectContextResult.IsSuccess)
        {
            return CreateExecutionErrorResponse(request, projectContextResult.Error!);
        }

        if (!requestDeadline.TryGetRemainingTimeout(out var timeout))
        {
            return CreateExecutionErrorResponse(
                request,
                ExecutionError.Timeout("Supervisor ensureRunning deadline expired before execution began."));
        }

        var timeoutMilliseconds = checked((int)Math.Ceiling(timeout.TotalMilliseconds));

        var requestLifetime = SupervisorRequestLifetime.Start(stream, cancellationToken);
        try
        {
            requestLifetimeStarted?.Invoke(requestLifetime);
            var progressObserver = streamWriter is null
                ? null
                : CreateProgressObserver(
                    streamWriter,
                    payload,
                    timeoutMilliseconds,
                    payload.EditorMode,
                    payload.OnStartupBlocked);

            DaemonStartResult startResult;
            try
            {
                startResult = await projectCoordinator.EnsureRunningAsync(
                        projectContextResult.Context!,
                        requestDeadline,
                        payload.EditorMode,
                        payload.OnStartupBlocked,
                        progressObserver,
                        requestLifetime.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (
                requestLifetime.IsCallerDisconnectCancellation
                || (exception.InnerException is not null
                && IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception.InnerException)))
            {
                return SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    ExecutionErrorCodes.IpcTimeout,
                    "Supervisor ensureRunning was canceled because the caller disconnected.");
            }

            if (!startResult.IsSuccess)
            {
                return CreateExecutionErrorResponse(
                    request,
                    startResult.Error!,
                    startResult.DaemonStatus,
                    startResult.Diagnosis,
                    startResult.Startup);
            }

            return SupervisorIpcResponseFactory.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: startResult.Status,
                    Session: DaemonSessionContractMapper.ToContract(startResult.Session!),
                    LifecycleObservation: startResult.LifecycleObservation));
        }
        finally
        {
            requestLifetime.Release();
        }
    }

    private async ValueTask<IpcResponse> HandleStopProjectAsync (
        Stream stream,
        ValidatedSupervisorIpcRequest request,
        ExecutionDeadline requestDeadline,
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        if (!IpcPayloadCodec.TryDeserialize(
                request.Payload,
                out SupervisorIpcContracts.StopProjectRequest payload,
                out var payloadError))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                $"Supervisor stopProject payload is invalid. {payloadError.Message}");
        }

        var projectContextResult = TryCreateProjectContext(
            runtimeContext,
            payload.UnityProjectRoot,
            payload.ProjectFingerprint);
        if (!projectContextResult.IsSuccess)
        {
            return CreateExecutionErrorResponse(request, projectContextResult.Error!);
        }

        if (!requestDeadline.TryGetRemainingTimeout(out _))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                ExecutionErrorCodes.IpcTimeout,
                $"Supervisor stopProject deadline expired before dispatch. DeadlineUtc={request.RequestDeadlineUtc:O}.");
        }

        var requestLifetime = SupervisorRequestLifetime.Start(stream, cancellationToken);
        try
        {
            DaemonStopResult stopResult;
            try
            {
                stopResult = await projectCoordinator.StopProjectAsync(
                        projectContextResult.Context!,
                        requestDeadline,
                        requestLifetime.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (requestLifetime.IsCallerDisconnectCancellation)
            {
                return SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    ExecutionErrorCodes.IpcTimeout,
                    "Supervisor stopProject was canceled because the caller disconnected.");
            }

            if (!stopResult.IsSuccess)
            {
                return CreateExecutionErrorResponse(request, stopResult.Error!);
            }

            return SupervisorIpcResponseFactory.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.StopProjectResponse(
                    StopStatus: stopResult.Status.Value));
        }
        finally
        {
            requestLifetime.Release();
        }
    }

    private static SupervisorIpcRequestValidationResult ValidateRequest (
        IpcRequestEnvelope request,
        SupervisorRuntimeContext runtimeContext)
    {
        var hasResponseMode = ContractLiteralCodec.TryParse(
            request.ResponseMode,
            out IpcResponseMode responseMode);
        var errorResponseMode = hasResponseMode
            ? responseMode
            : IpcResponseMode.Single;

        if (string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return SupervisorIpcRequestValidationResult.Failure(
                SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcSessionErrorCodes.SessionTokenRequired,
                    "Supervisor session token is required."),
                errorResponseMode);
        }

        if (!IpcSessionToken.TryParse(request.SessionToken, out var presentedSessionToken)
            || presentedSessionToken != runtimeContext.Manifest.SessionToken)
        {
            return SupervisorIpcRequestValidationResult.Failure(
                SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Supervisor session token is invalid."),
                errorResponseMode);
        }

        if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            return SupervisorIpcRequestValidationResult.Failure(
                SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcProtocolErrorCodes.ProtocolVersionMismatch,
                    $"Protocol version mismatch. Requested={request.ProtocolVersion}, Supported={IpcProtocol.CurrentVersion}."),
                errorResponseMode);
        }

        if (!hasResponseMode)
        {
            return SupervisorIpcRequestValidationResult.Failure(
                SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Unsupported supervisor IPC response mode."),
                IpcResponseMode.Single);
        }

        if (!ContractLiteralCodec.TryParse(request.Method, out SupervisorIpcMethod method))
        {
            return SupervisorIpcRequestValidationResult.Failure(
                SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcProtocolErrorCodes.IpcMethodNotSupported,
                    "Supervisor IPC method is not supported."),
                responseMode);
        }

        return SupervisorIpcRequestValidationResult.Success(
            new ValidatedSupervisorIpcRequest(
                request.RequestId,
                method,
                request.Payload,
                responseMode,
                request.RequestDeadlineUtc,
                request.RequestDeadlineRemainingMilliseconds));
    }

    private ExecutionDeadline? StartRequestDeadline (IpcRequestEnvelope request)
    {
        var startTimestamp = timeProvider.GetTimestamp();
        var observedAtUtc = timeProvider.GetUtcNow();
        var absoluteRemaining = request.RequestDeadlineUtc - observedAtUtc;
        var monotonicRemaining = TimeSpan.FromMilliseconds(request.RequestDeadlineRemainingMilliseconds);
        var effectiveRemaining = absoluteRemaining < monotonicRemaining
            ? absoluteRemaining
            : monotonicRemaining;
        return effectiveRemaining > TimeSpan.Zero
            ? ExecutionDeadline.StartFromObservation(
                effectiveRemaining,
                observedAtUtc,
                startTimestamp,
                timeProvider)
            : null;
    }

    private async Task TryWriteResponseAsync (
        Stream stream,
        IpcResponse response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var writeTimeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeoutTask = Task.Delay(
                SupervisorConstants.ResponseFrameWriteTimeout,
                timeProvider,
                writeTimeoutCancellationTokenSource.Token);
            var writeTask = Task.Run(
                async () => await IpcFrameCodec.WriteModelAsync(
                        stream,
                        response,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false),
                CancellationToken.None);
            var completedTask = await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, writeTask))
            {
                ObserveFault(writeTask);
                return;
            }

            writeTimeoutCancellationTokenSource.Cancel();
            await writeTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // NOTE:
            // the peer may already close the connection after sending a malformed frame.
        }
    }

    private async ValueTask TryWriteTerminalAsync (
        IpcStreamFrameWriter streamWriter,
        IpcResponse response,
        CancellationToken cancellationToken)
    {
        try
        {
            await streamWriter.WriteTerminalAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
        {
            // NOTE: A broken response stream is connection-local; the supervisor listener must keep serving.
        }
    }

    private static IDaemonStartProgressObserver CreateProgressObserver (
        IpcStreamFrameWriter streamWriter,
        SupervisorIpcContracts.EnsureRunningRequest payload,
        int timeoutMilliseconds,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        return new DaemonStartProgressEmitter(
            new SupervisorIpcCommandProgressSink(streamWriter),
            payload.ProjectFingerprint,
            timeoutMilliseconds,
            editorMode,
            onStartupBlocked);
    }

    private static ProjectContextResult TryCreateProjectContext (
        SupervisorRuntimeContext runtimeContext,
        string unityProjectRoot,
        ProjectFingerprint projectFingerprint)
    {
        if (projectFingerprint == null)
        {
            return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                "Project fingerprint must not be null."));
        }

        if (!AbsolutePath.TryParse(unityProjectRoot, out var normalizedUnityProjectRoot, out var projectRootFailure))
        {
            return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                $"Unity project root path is invalid. {projectRootFailure.Message}"));
        }

        var expectedFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            normalizedUnityProjectRoot);
        if (expectedFingerprint != projectFingerprint)
        {
            return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                "Project fingerprint does not match the specified Unity project root."));
        }

        return ProjectContextResult.Success(ResolvedUnityProjectContext.Create(
            unityProjectRoot: normalizedUnityProjectRoot,
            repositoryRoot: runtimeContext.StorageRoot,
            projectFingerprint: projectFingerprint,
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion));
    }

    private static IpcResponse CreateExecutionErrorResponse (
        ValidatedSupervisorIpcRequest request,
        ExecutionError error)
    {
        return SupervisorIpcResponseFactory.CreateErrorResponse(
            request,
            ExecutionErrorCodeMapper.ToCode(error),
            error.Message);
    }

    private static IpcResponse CreateExecutionErrorResponse (
        ValidatedSupervisorIpcRequest request,
        ExecutionError error,
        DaemonStatusKind daemonStatus,
        DaemonDiagnosis? diagnosis,
        DaemonStartupObservation? startup)
    {
        return SupervisorIpcResponseFactory.CreateErrorResponse(
            request,
            ExecutionErrorCodeMapper.ToCode(error),
            error.Message,
            IpcPayloadCodec.SerializeToElement(SupervisorEnsureRunningFailurePayloadMapper.ToContract(
                daemonStatus,
                diagnosis,
                startup)));
    }

    private static void ObserveFault (Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private sealed record ProjectContextResult (
        ResolvedUnityProjectContext? Context,
        ExecutionError? Error)
    {
        public bool IsSuccess => Context is not null && Error is null;

        public static ProjectContextResult Success (ResolvedUnityProjectContext context)
        {
            return new ProjectContextResult(context, null);
        }

        public static ProjectContextResult Failure (ExecutionError error)
        {
            return new ProjectContextResult(null, error);
        }
    }
}
