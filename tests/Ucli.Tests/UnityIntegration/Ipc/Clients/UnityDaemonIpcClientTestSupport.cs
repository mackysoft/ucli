using System.Text.Json;
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
        return new UnityIpcDispatchRequest(UnityIpcMethod.OpsRead, CreateDispatchPayload());
    }

    public static IpcResponse CreateResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcProtocol.StatusOk,
            payload: EmptyPayload(),
            errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateSessionTokenInvalidResponse ()
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcProtocol.StatusError,
            payload: CreateDispatchPayload(),
            errors:
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The daemon session token rotated during endpoint recovery.",
                    null),
            ]);
    }

    public static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            IpcSessionTokenTestFactory.Create(sessionToken),
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-session.sock")));
    }

    public static UnityDaemonRecoveryWaiter CreateRecoveryWaiter (DaemonSession session)
    {
        return new UnityDaemonRecoveryWaiter(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
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
            editorInstanceId: session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)));
    }
}
