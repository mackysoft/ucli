using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class UnityIpcRequestExecutorTestSupport
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static IUnityIpcClient[] CreateClients (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        IDaemonSessionConnectionProvider sessionConnectionProvider,
        IUnityBatchmodeProcessLauncher launcher)
    {
        return
        [
            new UnityDaemonIpcClient(
                daemonTransportClient,
                sessionConnectionProvider,
                recoveryWaiter: null,
                timeProvider: TimeProvider.System),
            UnityOneshotIpcClientTestSupport.CreateClient(
                launcher,
                oneshotTransportClient,
                new StubProjectLifecycleLockProvider(),
                new RecordingUnityProjectLockPreflightService()),
        ];
    }

    public static UnityIpcRequestExecutor CreateExecutor (
        IUnityExecutionModeDecisionService modeDecisionService,
        IDaemonPingInfoClient daemonPingInfoClient,
        IUnityUcliPluginLocator pluginLocator,
        IUnityIpcClient[] clients,
        TimeProvider? timeProvider = null)
    {
        return new UnityIpcRequestExecutor(
            new UnityIpcRequestBuilder(),
            new UnityIpcExecutionTargetResolver(
                modeDecisionService,
                new UnityIpcPluginVerifier(pluginLocator)),
            new UnityIpcClientSelector(clients),
            new UnityDaemonReadinessGate(daemonPingInfoClient, timeProvider ?? TimeProvider.System),
            timeProvider ?? TimeProvider.System);
    }

    public static UnityRequestPayload.OpsRead CreateOpsReadPayload ()
    {
        return new UnityRequestPayload.OpsRead();
    }

    public static UnityRequestPayload.OpsRead CreateOpsReadPayload (
        bool failFast,
        bool requireReadinessGate)
    {
        return new UnityRequestPayload.OpsRead(
            FailFast: failFast,
            RequireReadinessGate: requireReadinessGate);
    }

    public static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
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

    public static IpcResponse CreateReadyPingResponse (
        Guid requestId,
        ProjectFingerprint projectFingerprint)
    {
        var payload = IpcPayloadCodec.SerializeToElement(CreatePingPayload(
            IpcEditorLifecycleState.Ready,
            projectFingerprint));
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Ok,
            payload: payload,
            errors: Array.Empty<IpcError>());
    }

    public static IpcUnityEditorObservation CreatePingPayload (
        IpcEditorLifecycleState lifecycleState,
        ProjectFingerprint? projectFingerprint = null)
    {
        return IpcUnityEditorObservationTestFactory.Create(
            lifecycleState,
            projectFingerprint: projectFingerprint);
    }

    public static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            IpcSessionTokenTestFactory.Create(sessionToken),
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-session.sock")));
    }

    public static void AssertSuccessfulUnityResponse (
        IpcResponse expected,
        UnityRequestResponse? actual)
    {
        Assert.NotNull(actual);
        Assert.Empty(actual!.Errors);
        Assert.Equal(expected.Payload.GetRawText(), actual.Payload.GetRawText());
        Assert.Equal(expected.Errors.Count, actual.Errors.Count);
        for (var i = 0; i < expected.Errors.Count; i++)
        {
            Assert.Equal(expected.Errors[i].Code, actual.Errors[i].Code);
            Assert.Equal(expected.Errors[i].Message, actual.Errors[i].Message);
            Assert.Equal(expected.Errors[i].OpId, actual.Errors[i].OpId);
        }
    }
}
