using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Validates and dispatches supervisor IPC requests to the appropriate lifecycle coordinator. </summary>
internal sealed class SupervisorRequestDispatcher
{
    private readonly SupervisorActivityTracker activityTracker;

    private readonly SupervisorProjectCoordinator projectCoordinator;

    /// <summary> Initializes a new instance of the <see cref="SupervisorRequestDispatcher" /> class. </summary>
    /// <param name="activityTracker"> The supervisor activity-tracker dependency. </param>
    /// <param name="projectCoordinator"> The supervisor project-coordinator dependency. </param>
    public SupervisorRequestDispatcher (
        SupervisorActivityTracker activityTracker,
        SupervisorProjectCoordinator projectCoordinator)
    {
        this.activityTracker = activityTracker ?? throw new ArgumentNullException(nameof(activityTracker));
        this.projectCoordinator = projectCoordinator ?? throw new ArgumentNullException(nameof(projectCoordinator));
    }

    /// <summary> Handles one supervisor IPC connection. </summary>
    /// <param name="stream"> The accepted transport stream. </param>
    /// <param name="runtimeContext"> The immutable supervisor runtime context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the listener. </param>
    public async Task HandleConnection (
        Stream stream,
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(runtimeContext);

        activityTracker.Touch();

        var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            var malformedResponse = SupervisorIpcResponseFactory.CreateMalformedFrameResponse(
                readResult.ErrorKind,
                readResult.ErrorMessage);
            await TryWriteResponse(stream, malformedResponse, cancellationToken).ConfigureAwait(false);
            return;
        }

        var response = await ProcessRequest(stream, runtimeContext, readResult.Value, cancellationToken).ConfigureAwait(false);
        await TryWriteResponse(stream, response, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IpcResponse> ProcessRequest (
        Stream stream,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        using var requestScope = activityTracker.BeginRequest();

        if (string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.SessionTokenRequired,
                "Supervisor session token is required.");
        }

        if (!string.Equals(request.SessionToken, runtimeContext.Manifest.SessionToken, StringComparison.Ordinal))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.SessionTokenInvalid,
                "Supervisor session token is invalid.");
        }

        if (request.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.ProtocolVersionMismatch,
                $"Protocol version mismatch. Requested={request.ProtocolVersion}, Supported={IpcProtocol.CurrentVersion}.");
        }

        return request.Method switch
        {
            SupervisorIpcContracts.PingMethod => HandlePing(request, runtimeContext),
            SupervisorIpcContracts.EnsureRunningMethod => await HandleEnsureRunning(stream, request, runtimeContext, cancellationToken).ConfigureAwait(false),
            SupervisorIpcContracts.StopProjectMethod => await HandleStopProject(stream, request, runtimeContext, cancellationToken).ConfigureAwait(false),
            _ => SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.IpcMethodNotSupported,
                $"Supervisor IPC method is not supported: {request.Method}."),
        };
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

    private async ValueTask<IpcResponse> HandleEnsureRunning (
        Stream stream,
        IpcRequest request,
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        if (!IpcPayloadCodec.TryDeserialize(
                request.Payload,
                out SupervisorIpcContracts.EnsureRunningRequest payload,
                out var payloadError))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InvalidArgument,
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

        var timeout = TimeSpan.FromMilliseconds(payload.TimeoutMilliseconds);
        if (timeout <= TimeSpan.Zero)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InvalidArgument,
                $"Supervisor ensureRunning timeout must be greater than zero. Actual={payload.TimeoutMilliseconds}.");
        }

        await using var requestLifetime = SupervisorRequestLifetime.Start(stream, timeout, cancellationToken);

        DaemonStartResult startResult;
        try
        {
            startResult = await projectCoordinator.EnsureRunning(
                    projectContextResult.Context!,
                    timeout,
                    requestLifetime.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (requestLifetime.IsTimeoutCancellation)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                ExecutionErrorCodes.IpcTimeout,
                $"Supervisor ensureRunning timed out after {payload.TimeoutMilliseconds} milliseconds.");
        }
        catch (OperationCanceledException) when (requestLifetime.IsCallerDisconnectCancellation)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                ExecutionErrorCodes.IpcTimeout,
                "Supervisor ensureRunning was canceled because the caller disconnected.");
        }

        if (!startResult.IsSuccess)
        {
            return CreateExecutionErrorResponse(request, startResult.Error!);
        }

        if (!DaemonStartStateCodec.TryToValue(startResult.Status, out var startStatus))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InternalError,
                $"Supervisor ensureRunning returned unsupported start status: {startResult.Status}.");
        }

        return SupervisorIpcResponseFactory.CreateSuccessResponse(
            request,
            new SupervisorIpcContracts.EnsureRunningResponse(
                StartStatus: startStatus!,
                DaemonStatus: DaemonStatusStateCodec.Running,
                Session: startResult.Session!));
    }

    private async ValueTask<IpcResponse> HandleStopProject (
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
                IpcErrorCodes.InvalidArgument,
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

        var timeout = TimeSpan.FromMilliseconds(payload.TimeoutMilliseconds);
        if (timeout <= TimeSpan.Zero)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InvalidArgument,
                $"Supervisor stopProject timeout must be greater than zero. Actual={payload.TimeoutMilliseconds}.");
        }

        await using var requestLifetime = SupervisorRequestLifetime.Start(stream, timeout, cancellationToken);

        DaemonStopResult stopResult;
        try
        {
            stopResult = await projectCoordinator.StopProject(
                    projectContextResult.Context!,
                    timeout,
                    requestLifetime.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (requestLifetime.IsTimeoutCancellation)
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                ExecutionErrorCodes.IpcTimeout,
                $"Supervisor stopProject timed out after {payload.TimeoutMilliseconds} milliseconds.");
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

        if (!DaemonStopStateCodec.TryToValue(stopResult.Status, out var stopStatus))
        {
            return SupervisorIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InternalError,
                $"Supervisor stopProject returned unsupported stop status: {stopResult.Status}.");
        }

        return SupervisorIpcResponseFactory.CreateSuccessResponse(
            request,
            new SupervisorIpcContracts.StopProjectResponse(
                StopStatus: stopStatus!,
                DaemonStatus: DaemonStatusStateCodec.NotRunning));
    }

    private async Task TryWriteResponse (
        Stream stream,
        IpcResponse response,
        CancellationToken cancellationToken)
    {
        try
        {
            await IpcFrameCodec.WriteModelAsync(
                    stream,
                    response,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // NOTE:
            // the peer may already close the connection after sending a malformed frame.
        }
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
            var normalizedUnityProjectRoot = Path.GetFullPath(unityProjectRoot);
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
            ExecutionErrorCodeMapper.ToCode(error.Kind),
            error.Message);
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
