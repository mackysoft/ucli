using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Acquisition;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
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

    public static UnityIpcDispatchRequest CreateLifecycleDispatchRequest (
        LifecycleExecutionKind kind,
        TimeProvider? timeProvider = null,
        TimeSpan? executionTimeout = null)
    {
        var registration = UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
            kind,
            timeProvider: timeProvider,
            executionTimeout: executionTimeout);
        var payload = kind switch
        {
            LifecycleExecutionKind.Refresh =>
                (UnityRequestPayload)new UnityRequestPayload.Refresh(
                    registration,
                    requiredStart: null,
                    new RefreshLifecycleExecutionStartAdmissionPolicy(
                        failFast: false)),
            LifecycleExecutionKind.Compile =>
                new UnityRequestPayload.Compile(
                    registration,
                    requiredStart: null),
            LifecycleExecutionKind.PlayEnter =>
                new UnityRequestPayload.PlayEnter(
                    registration,
                    requiredStart: null),
            LifecycleExecutionKind.PlayExit =>
                new UnityRequestPayload.PlayExit(
                    registration,
                    requiredStart: null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported Lifecycle Execution kind."),
        };
        return new UnityIpcRequestBuilder().Build(payload);
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
        TimeProvider timeProvider,
        TimeSpan? recoveryLeaseDuration = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new DaemonSessionRecoveryWaiter(
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(
                    CreateRecoveringObservation(
                        session,
                        timeProvider.GetUtcNow(),
                        recoveryLeaseDuration)),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
    }

    public static void AssertUnityResponse (
        IpcResponse expected,
        UnityRequestResponse? actual)
    {
        Assert.NotNull(actual);
        Assert.Empty(actual!.Errors);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(expected.Payload.GetRawText()),
            JsonNode.Parse(actual.Payload.GetRawText())));
        Assert.Equal(expected.Errors.Count, actual.Errors.Count);
        for (var i = 0; i < expected.Errors.Count; i++)
        {
            Assert.Equal(expected.Errors[i].Code, actual.Errors[i].Code);
            Assert.Equal(expected.Errors[i].Message, actual.Errors[i].Message);
            Assert.Equal(expected.Errors[i].InstancePath, actual.Errors[i].InstancePath);
        }
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (
        DaemonSession session,
        DateTimeOffset observedAtUtc,
        TimeSpan? recoveryLeaseDuration)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: session.EditorMode,
                lifecycleState: recoveryLeaseDuration.HasValue
                    ? UnityEditorLifecycleState.Recovering
                    : UnityEditorLifecycleState.DomainReloading,
                compileState: UnityEditorCompileState.Ready,
                generations: new UnityEditorGenerationSnapshot(1, 2, 0, 0),
                playMode: new UnityEditorPlayModeSnapshot(
                    UnityEditorPlayModeState.Stopped,
                    UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: observedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)),
            recoveryLease: recoveryLeaseDuration is TimeSpan duration
                ? new DaemonLifecycleRecoveryLease(
                    session.SessionGenerationId,
                    observedAtUtc + duration)
                : null);
    }
}
