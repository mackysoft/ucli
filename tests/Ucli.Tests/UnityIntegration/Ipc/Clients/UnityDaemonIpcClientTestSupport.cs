using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class UnityDaemonIpcClientTestSupport
{
    public static JsonElement CreateDispatchPayload ()
    {
        return JsonDocument.Parse("""{"sentinel":"daemon-payload"}""").RootElement.Clone();
    }

    public static UnityIpcDispatchRequest CreateDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(IpcMethodNames.OpsRead, CreateDispatchPayload());
    }

    public static IpcResponse CreateResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: EmptyPayload(),
            Errors: Array.Empty<IpcError>());
    }

    public static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            sessionToken,
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-session.sock")));
    }

    public static UnityDaemonRecoveryWaiter CreateRecoveryWaiter (
        DaemonSession session,
        ManualTimeProvider timeProvider)
    {
        return new UnityDaemonRecoveryWaiter(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Success(session),
            },
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess),
            timeProvider);
    }

    public static void AssertUnityResponse (
        IpcResponse expected,
        UnityRequestResponse? actual)
    {
        Assert.NotNull(actual);
        Assert.False(actual!.HasFailureStatus);
        Assert.Equal(expected.Payload.GetRawText(), actual.Payload.GetRawText());
        Assert.Equal(expected.Errors.Count, actual.Errors.Count);
        for (var i = 0; i < expected.Errors.Count; i++)
        {
            Assert.Equal(expected.Errors[i].Code, actual.Errors[i].Code);
            Assert.Equal(expected.Errors[i].Message, actual.Errors[i].Message);
            Assert.Equal(expected.Errors[i].OpId, actual.Errors[i].OpId);
        }
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (DaemonSession session)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: session.EditorMode,
                lifecycleState: IpcEditorLifecycleState.DomainReloading,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero),
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: session.EditorInstanceId);
    }
}
