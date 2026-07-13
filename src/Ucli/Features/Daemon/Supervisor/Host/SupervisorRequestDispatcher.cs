using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
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
            () => IpcFrameCodec.TryReadModelAsync<IpcRequest>(
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

        var request = readResult.Value;
        if (!TryParseResponseMode(request, out var responseMode, out var responseModeError))
        {
            await TryWriteResponseAsync(stream, responseModeError!, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (responseMode == IpcResponseMode.Stream)
        {
            await HandleStreamingRequestAsync(stream, runtimeContext, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var response = await ProcessRequestAsync(
                stream,
                runtimeContext,
                request,
                streamWriter: null,
                requestLifetimeStarted: null,
                cancellationToken)
            .ConfigureAwait(false);
        await TryWriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IpcResponse> ProcessRequestAsync (
        Stream stream,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request,
        IpcStreamFrameWriter? streamWriter,
        Action<SupervisorRequestLifetime>? requestLifetimeStarted,
        CancellationToken cancellationToken)
    {
        using var requestScope = activityTracker.BeginRequest();

        if (string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcSessionErrorCodes.SessionTokenRequired,
                "Supervisor session token is required.");
        }

        if (!IpcSessionToken.IsValidEncodedValue(request.SessionToken)
            || !runtimeContext.Manifest.SessionToken.Matches(request.SessionToken))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcSessionErrorCodes.SessionTokenInvalid,
                "Supervisor session token is invalid.");
        }

        if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcProtocolErrorCodes.ProtocolVersionMismatch,
                $"Protocol version mismatch. Requested={request.ProtocolVersion}, Supported={IpcProtocol.CurrentVersion}.");
        }

        if (!ContractLiteralCodec.TryParse(request.Method, out SupervisorIpcMethod method))
        {
            var methodLiteral = request.Method ?? "<null>";
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcProtocolErrorCodes.IpcMethodNotSupported,
                $"Supervisor IPC method is not supported: {methodLiteral}.");
        }

        if (streamWriter is not null
            && method != SupervisorIpcMethod.EnsureRunning)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                $"Supervisor IPC responseMode 'stream' is only supported for {ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning)}.");
        }

        return method switch
        {
            SupervisorIpcMethod.Ping => HandlePing(request, runtimeContext),
            SupervisorIpcMethod.EnsureRunning => await HandleEnsureRunningAsync(
                    stream,
                    request,
                    runtimeContext,
                    streamWriter,
                    requestLifetimeStarted,
                    cancellationToken)
                .ConfigureAwait(false),
            SupervisorIpcMethod.StopProject => await HandleStopProjectAsync(stream, request, runtimeContext, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported supervisor IPC method: {method}."),
        };
    }

    private async ValueTask HandleStreamingRequestAsync (
        Stream stream,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        SupervisorRequestLifetime? activeRequestLifetime = null;
        var streamWriter = new IpcStreamFrameWriter(
            stream,
            request,
            cancellationToken,
            cancellationToken,
            SupervisorConstants.ResponseFrameWriteTimeout,
            _ => activeRequestLifetime?.CancelForResponseStreamFailure());
        var response = await ProcessRequestAsync(
                stream,
                runtimeContext,
                request,
                streamWriter,
                requestLifetime => activeRequestLifetime = requestLifetime,
                cancellationToken)
            .ConfigureAwait(false);
        await TryWriteTerminalAsync(streamWriter, response, cancellationToken).ConfigureAwait(false);
    }

    private static IpcResponse HandlePing (
        IpcRequest request,
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
        IpcRequest request,
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

        if (payload.DeadlineUtc == default)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                "Supervisor ensureRunning deadlineUtc must be specified.");
        }

        if (payload.AttemptTimeoutMilliseconds <= 0)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                "Supervisor ensureRunning attemptTimeoutMilliseconds must be greater than zero.");
        }

        var absoluteRemaining = payload.DeadlineUtc - timeProvider.GetUtcNow();
        if (absoluteRemaining <= TimeSpan.Zero)
        {
            return CreateExecutionErrorResponse(
                request,
                ExecutionError.Timeout("Supervisor ensureRunning deadline expired before execution began."));
        }

        var attemptTimeout = TimeSpan.FromMilliseconds(payload.AttemptTimeoutMilliseconds);
        var timeout = absoluteRemaining < attemptTimeout
            ? absoluteRemaining
            : attemptTimeout;
        var timeoutMilliseconds = checked((int)Math.Ceiling(timeout.TotalMilliseconds));

        var editorMode = (DaemonEditorMode?)null;
        if (payload.EditorMode != null)
        {
            if (!ContractLiteralInputParser.TryParseTrimmed<DaemonEditorMode>(payload.EditorMode, out var parsedEditorMode))
            {
                return SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    $"Supervisor ensureRunning editorMode is invalid. Actual={payload.EditorMode}.");
            }

            editorMode = parsedEditorMode;
        }

        if (!ContractLiteralInputParser.TryParseTrimmed<DaemonStartupBlockedProcessPolicy>(payload.OnStartupBlocked, out var onStartupBlocked))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                $"Supervisor ensureRunning onStartupBlocked is invalid. Actual={payload.OnStartupBlocked}.");
        }

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
                    editorMode,
                    onStartupBlocked);

            DaemonStartResult startResult;
            try
            {
                startResult = await projectCoordinator.EnsureRunningAsync(
                        projectContextResult.Context!,
                        timeout,
                        editorMode,
                        onStartupBlocked,
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

            if (!ContractLiteralCodec.TryToValue(startResult.Status, out var startStatus))
            {
                return SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Supervisor ensureRunning returned unsupported start status: {startResult.Status}.");
            }

            return SupervisorIpcResponseFactory.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: startStatus!,
                    DaemonStatus: ContractLiteralCodec.ToValue(DaemonStatusKind.Running),
                    Session: DaemonSessionContractMapper.ToContract(startResult.Session!),
                    LifecycleSnapshot: startResult.LifecycleSnapshot));
        }
        finally
        {
            requestLifetime.Release();
        }
    }

    private async ValueTask<IpcResponse> HandleStopProjectAsync (
        Stream stream,
        IpcRequest request,
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

        if (payload.DeadlineUtc == default)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                "Supervisor stopProject deadlineUtc is required.");
        }

        if (payload.AttemptTimeoutMilliseconds <= 0)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                "Supervisor stopProject attemptTimeoutMilliseconds must be greater than zero.");
        }

        var absoluteRemaining = payload.DeadlineUtc - timeProvider.GetUtcNow();
        if (absoluteRemaining <= TimeSpan.Zero)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                ExecutionErrorCodes.IpcTimeout,
                $"Supervisor stopProject deadline expired before dispatch. DeadlineUtc={payload.DeadlineUtc:O}.");
        }

        var attemptTimeout = TimeSpan.FromMilliseconds(payload.AttemptTimeoutMilliseconds);
        var timeout = absoluteRemaining < attemptTimeout
            ? absoluteRemaining
            : attemptTimeout;

        var requestLifetime = SupervisorRequestLifetime.Start(stream, cancellationToken);
        try
        {
            DaemonStopResult stopResult;
            try
            {
                stopResult = await projectCoordinator.StopProjectAsync(
                        projectContextResult.Context!,
                        timeout,
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

            if (!ContractLiteralCodec.TryToValue(stopResult.Status, out var stopStatus))
            {
                return SupervisorIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Supervisor stopProject returned unsupported stop status: {stopResult.Status}.");
            }

            return SupervisorIpcResponseFactory.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.StopProjectResponse(
                    StopStatus: stopStatus!,
                    DaemonStatus: ContractLiteralCodec.ToValue(DaemonStatusKind.NotRunning)));
        }
        finally
        {
            requestLifetime.Release();
        }
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

    private static bool TryParseResponseMode (
        IpcRequest request,
        out IpcResponseMode responseMode,
        out IpcResponse? responseModeError)
    {
        if (ContractLiteralCodec.TryParse(request.ResponseMode, out responseMode))
        {
            responseModeError = null;
            return true;
        }

        var responseModeLiteral = request.ResponseMode ?? "<null>";
        responseModeError = SupervisorIpcResponseFactory.CreateErrorResponse(
            request,
            UcliCoreErrorCodes.InvalidArgument,
            $"Unsupported IPC response mode: {responseModeLiteral}.");
        return false;
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
        string projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(unityProjectRoot))
        {
            return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                "Unity project root must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                "Project fingerprint must not be empty."));
        }

        try
        {
            var projectRootResult = PathNormalizer.TryNormalizeFullPath(unityProjectRoot);
            if (!projectRootResult.IsSuccess)
            {
                return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                    $"Unity project root path is invalid. {projectRootResult.DiagnosticMessage}"));
            }

            var normalizedUnityProjectRoot = projectRootResult.FullPath!;
            var expectedFingerprint = UnityProjectFingerprintCalculator.Create(
                runtimeContext.StorageRoot,
                normalizedUnityProjectRoot);
            if (!string.Equals(expectedFingerprint, projectFingerprint, StringComparison.Ordinal))
            {
                return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                    "Project fingerprint does not match the specified Unity project root."));
            }

            return ProjectContextResult.Success(new ResolvedUnityProjectContext(
                UnityProjectRoot: normalizedUnityProjectRoot,
                RepositoryRoot: runtimeContext.StorageRoot,
                ProjectFingerprint: projectFingerprint,
                PathSource: UnityProjectPathSource.CommandOption));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ProjectContextResult.Failure(ExecutionError.InvalidArgument(
                $"Unity project root path is invalid. {exception.Message}"));
        }
    }

    private static IpcResponse CreateExecutionErrorResponse (
        IpcRequest request,
        ExecutionError error)
    {
        return SupervisorIpcResponseFactory.CreateErrorResponse(
            request,
            ExecutionErrorCodeMapper.ToCode(error),
            error.Message);
    }

    private static IpcResponse CreateExecutionErrorResponse (
        IpcRequest request,
        ExecutionError error,
        DaemonStatusKind daemonStatus,
        DaemonDiagnosis? diagnosis,
        DaemonStartupObservation? startup)
    {
        var daemonStatusValue = ContractLiteralCodec.TryToValue(daemonStatus, out var value)
            ? value
            : ContractLiteralCodec.ToValue(DaemonStatusKind.NotRunning);
        return SupervisorIpcResponseFactory.CreateErrorResponse(
            request,
            ExecutionErrorCodeMapper.ToCode(error),
            error.Message,
            new SupervisorIpcContracts.EnsureRunningFailureResponse(daemonStatusValue, diagnosis, startup));
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
