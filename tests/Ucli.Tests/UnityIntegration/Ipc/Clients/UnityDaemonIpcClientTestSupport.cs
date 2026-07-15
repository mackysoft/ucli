using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class UnityDaemonIpcClientTestSupport
{
    public static JsonElement CreateDispatchPayload ()
    {
        return JsonDocument.Parse("""{"sentinel":"daemon-payload"}""").RootElement.Clone();
    }

    public static UnityIpcDispatchRequest CreateDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(
            UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
    }

    public static IpcResponse CreateResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Ok,
            payload: EmptyPayload(),
            errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateSessionTokenInvalidResponse ()
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: CreateDispatchPayload(),
            errors:
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The daemon session token rotated during endpoint recovery.",
                    null),
            ]);
    }

    public static DaemonSessionReadResult CreateSessionReadResult (string sessionToken)
    {
        return DaemonSessionReadResultTestFactory.FoundForToken(sessionToken);
    }

    public static DaemonSessionRecoveryWaiter CreateRecoveryWaiter (
        DaemonSession session,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new DaemonSessionRecoveryWaiter(
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(
                    CreateRecoveringObservation(session, timeProvider.GetUtcNow())),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
    }

    public static void AssertUnityResponse (
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

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (
        DaemonSession session,
        DateTimeOffset observedAtUtc)
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
            observedAtUtc: observedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)),
            recoveryLease: null);
    }
}
