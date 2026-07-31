using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class UnityOneshotIpcClientTestSupport
{
    public static readonly TimeSpan MaximumStartupRetryDelay = TimeSpan.FromSeconds(1);

    public static UnityOneshotIpcClient CreateClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        IUnityLogReader? unityLogReader = null,
        TimeSpan? cleanupTimeout = null,
        TimeSpan? cleanupRetryDelay = null,
        TimeProvider? timeProvider = null)
    {
        var defaultPolicy = UnityOneshotCleanupPolicy.Default;
        var cleanupPolicy = cleanupTimeout.HasValue || cleanupRetryDelay.HasValue
            ? new UnityOneshotCleanupPolicy(
                cleanupTimeout ?? defaultPolicy.Timeout,
                cleanupRetryDelay ?? defaultPolicy.RetryDelay)
            : defaultPolicy;
        return new UnityOneshotIpcClient(
            batchmodeProcessLauncher,
            transportClient,
            lifecycleLockProvider,
            unityProjectLockPreflightService,
            unityLogReader,
            timeProvider ?? TimeProvider.System,
            cleanupPolicy);
    }

    public static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    public static JsonElement CreateDispatchPayload ()
    {
        return JsonDocument.Parse("""{"sentinel":"oneshot-payload"}""").RootElement.Clone();
    }

    public static UnityIpcDispatchRequest CreateDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(
            UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
    }

    public static UnityIpcDispatchRequest CreateStreamingDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(
            UnityIpcMethod.TestRun,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
    }

    public static UnityIpcDispatchRequest CreateOpsReadDispatchRequest (
        bool failFast,
        bool requireReadinessGate)
    {
        return new UnityIpcDispatchRequest(
            UnityIpcMethod.OpsRead,
            IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(failFast, requireReadinessGate)),
            UnityBatchmodeLaunchOptions.Default);
    }

    public static UnityIpcDispatchRequest CreateReadyPingDispatchRequest (bool failFast)
    {
        return new UnityIpcDispatchRequest(
            UnityIpcMethod.Ping,
            IpcPayloadCodec.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.Ready, failFast)),
            UnityBatchmodeLaunchOptions.Default);
    }

    public static UnityIpcDispatchRequest CreateCompileDispatchRequest (
        TimeProvider? timeProvider = null,
        TimeSpan? executionTimeout = null)
    {
        var registration = UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
            LifecycleExecutionKind.Compile,
            RunIdTestValues.Compile,
            timeProvider,
            executionTimeout);
        return new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                requiredStart: null));
    }

    public static UnityIpcDispatchRequest CreateRefreshDispatchRequest (
        bool failFast,
        TimeProvider? timeProvider = null,
        TimeSpan? executionTimeout = null)
    {
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Refresh,
                timeProvider: timeProvider,
                executionTimeout: executionTimeout);
        return new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Refresh(
                registration,
                requiredStart: null,
                new RefreshLifecycleExecutionStartAdmissionPolicy(
                    failFast)));
    }

    public static IpcResponse CreateSuccessResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Ok,
            payload: EmptyPayload(),
            errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateErrorResponse (
        Guid requestId,
        UcliCode errorCode,
        string message)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Error,
            payload: EmptyPayload(),
            errors:
            [
                new IpcError(errorCode, message, null),
            ]);
    }

    public static IpcResponse CreateShutdownResponse (Guid requestId)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownResponse(
            Accepted: true,
            Message: "Shutdown request accepted."));
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Ok,
            payload: payload,
            errors: Array.Empty<IpcError>());
    }

    public static void AssertTerminalPingAndFallbackCleanupShutdownRequests (RecordingUnityIpcTransportClient transportClient)
    {
        var shutdownRequests = IpcRequestAssert.WithMethod(transportClient, UnityIpcMethod.Shutdown);
        Assert.Collection(
            shutdownRequests,
            AssertOneshotCleanupShutdownRequest,
            AssertOneshotCleanupShutdownRequest);
        Assert.Single(shutdownRequests.Select(static request => request.SessionToken).Distinct(StringComparer.Ordinal));
        Assert.All(shutdownRequests, request => Assert.NotEqual(Guid.Empty, request.RequestId));
        Assert.NotEqual(shutdownRequests[0].RequestId, shutdownRequests[1].RequestId);
    }

    public static IpcOneshotBootstrapEnvelope AssertCleanupShutdownUsesLaunchSession (
        RecordingUnityBatchmodeProcessLauncher launcher,
        RecordingUnityIpcTransportClient transportClient,
        ResolvedUnityProjectContext unityProject,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapEnvelope = UnityOneshotLaunchAssert.LaunchedOnce(launcher, unityProject, exitDeadlineReferenceUtc);
        var shutdownRequest = IpcRequestAssert.SingleWithMethod(transportClient, UnityIpcMethod.Shutdown);
        Assert.Equal(bootstrapEnvelope.SessionToken.GetEncodedValue(), shutdownRequest.SessionToken);
        return bootstrapEnvelope;
    }

    public static IpcOneshotBootstrapEnvelope AssertCleanupShutdownsUseLaunchSession (
        RecordingUnityBatchmodeProcessLauncher launcher,
        RecordingUnityIpcTransportClient transportClient,
        ResolvedUnityProjectContext unityProject,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapEnvelope = UnityOneshotLaunchAssert.LaunchedOnce(launcher, unityProject, exitDeadlineReferenceUtc);
        Assert.All(
            IpcRequestAssert.WithMethod(transportClient, UnityIpcMethod.Shutdown),
            request => Assert.Equal(bootstrapEnvelope.SessionToken.GetEncodedValue(), request.SessionToken));
        return bootstrapEnvelope;
    }

    public static IpcResponse CreatePingResponse (
        Guid requestId,
        UnityEditorLifecycleState lifecycleState = UnityEditorLifecycleState.Ready,
        ProjectFingerprint? projectFingerprint = null)
    {
        var payload = IpcPayloadCodec.SerializeToElement(UnityEditorObservationTestFactory.Create(
            lifecycleState: lifecycleState,
            projectFingerprint: projectFingerprint ?? ProjectFingerprintTestFactory.Create("project-fingerprint")));
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Ok,
            payload: payload,
            errors: Array.Empty<IpcError>());
    }

    public static RecordingUnityProjectLockPreflightService CreateProjectLockPreflightService (
        UnityProjectLockFileProbeResult? result = null)
    {
        if (result is null)
        {
            return new RecordingUnityProjectLockPreflightService();
        }

        return new RecordingUnityProjectLockPreflightService(ConvertProbeResult(result))
        {
            CleanupResult = ConvertPostExitProbeResult(result),
        };
    }

    private static void AssertOneshotCleanupShutdownRequest (IpcRequestEnvelope request)
    {
        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out IpcShutdownRequest payload,
            out _));
        Assert.Equal("ucli-oneshot-cleanup", payload.RequestedBy);
    }

    private static UnityProjectLockPreflightResult ConvertProbeResult (
        UnityProjectLockFileProbeResult result)
    {
        if (!result.IsSuccess)
        {
            return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
        }

        if (!result.IsLocked)
        {
            return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
        }

        return UnityProjectLockPreflightResult.ActiveLock(
            result.LockFilePath!,
            "Unity project is already open.");
    }

    private static UnityProjectLockPreflightResult ConvertPostExitProbeResult (UnityProjectLockFileProbeResult result)
    {
        if (!result.IsSuccess)
        {
            return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
        }

        if (!result.IsLocked)
        {
            return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
        }

        return UnityProjectLockPreflightResult.StaleLockCleared(result.LockFilePath!);
    }
}
