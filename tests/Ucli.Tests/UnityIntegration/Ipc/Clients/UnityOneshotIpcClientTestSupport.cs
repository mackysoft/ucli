using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class UnityOneshotIpcClientTestSupport
{
    public static readonly TimeSpan MaximumStartupRetryDelay = TimeSpan.FromSeconds(1);

    public static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    public static JsonElement CreateDispatchPayload ()
    {
        return JsonDocument.Parse("""{"sentinel":"oneshot-payload"}""").RootElement.Clone();
    }

    public static UnityIpcDispatchRequest CreateDispatchRequest (
        IpcResponseMode responseMode = IpcResponseMode.Single)
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.OpsRead,
            CreateDispatchPayload(),
            responseMode: responseMode);
    }

    public static UnityIpcDispatchRequest CreateOpsReadDispatchRequest (
        bool failFast,
        bool requireReadinessGate)
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.OpsRead,
            IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(failFast, requireReadinessGate)));
    }

    public static UnityIpcDispatchRequest CreateReadyPingDispatchRequest (bool failFast)
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.Ping,
            IpcPayloadCodec.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.Ready, failFast)));
    }

    public static UnityIpcDispatchRequest CreateCompileDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.Compile,
            IpcPayloadCodec.SerializeToElement(new IpcCompileRequest("compile-run-1")),
            [IpcEditorLifecycleStateCodec.CompileFailed, IpcEditorLifecycleStateCodec.SafeMode]);
    }

    public static IpcResponse CreateSuccessResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: EmptyPayload(),
            Errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateShutdownResponse (string requestId)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownResponse(
            Accepted: true,
            Message: "Shutdown request accepted."));
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: payload,
            Errors: Array.Empty<IpcError>());
    }

    public static void AssertTerminalPingAndCleanupShutdownRequests (RecordingUnityIpcTransportClient transportClient)
    {
        var shutdownRequests = IpcRequestAssert.WithMethod(transportClient, IpcMethodNames.Shutdown);
        Assert.Collection(
            shutdownRequests,
            AssertOneshotCleanupShutdownRequest,
            AssertOneshotCleanupShutdownRequest);
        Assert.Single(shutdownRequests.Select(static request => request.SessionToken).Distinct(StringComparer.Ordinal));
    }

    public static IpcOneshotBootstrapArguments AssertCleanupShutdownUsesLaunchSession (
        RecordingUnityBatchmodeProcessLauncher launcher,
        RecordingUnityIpcTransportClient transportClient,
        ResolvedUnityProjectContext unityProject,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapArguments = UnityOneshotLaunchAssert.LaunchedOnce(launcher, unityProject, exitDeadlineReferenceUtc);
        var shutdownRequest = IpcRequestAssert.SingleWithMethod(transportClient, IpcMethodNames.Shutdown);
        Assert.Equal(bootstrapArguments.SessionToken, shutdownRequest.SessionToken);
        return bootstrapArguments;
    }

    public static IpcOneshotBootstrapArguments AssertCleanupShutdownsUseLaunchSession (
        RecordingUnityBatchmodeProcessLauncher launcher,
        RecordingUnityIpcTransportClient transportClient,
        ResolvedUnityProjectContext unityProject,
        DateTimeOffset? exitDeadlineReferenceUtc = null)
    {
        var bootstrapArguments = UnityOneshotLaunchAssert.LaunchedOnce(launcher, unityProject, exitDeadlineReferenceUtc);
        Assert.All(
            IpcRequestAssert.WithMethod(transportClient, IpcMethodNames.Shutdown),
            request => Assert.Equal(bootstrapArguments.SessionToken, request.SessionToken));
        return bootstrapArguments;
    }

    public static IpcResponse CreatePingResponse (
        string requestId,
        string lifecycleState = IpcEditorLifecycleStateCodec.Ready,
        bool canAcceptExecutionRequests = true,
        string projectFingerprint = "project-fingerprint")
    {
        var payload = IpcPayloadCodec.SerializeToElement(IpcPingResponseTestFactory.Create(
            lifecycleState: lifecycleState,
            canAcceptExecutionRequests: canAcceptExecutionRequests,
            projectFingerprint: projectFingerprint));
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: payload,
            Errors: Array.Empty<IpcError>());
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

    private static void AssertOneshotCleanupShutdownRequest (IpcRequest request)
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
