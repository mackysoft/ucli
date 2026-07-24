using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherEnsureRunningTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenRequestDeliveryIsDelayed_UsesDeadlineRemainingAtReceipt ()
    {
        var timeProvider = new ManualTimeProvider();
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(new
                {
                    UnityProjectRoot = unityProjectRoot,
                    ProjectFingerprint = projectFingerprint,
                    EditorMode = (string?)null,
                    OnStartupBlocked = "auto",
                }),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: deadlineUtc,
                requestDeadlineRemainingMilliseconds: 800));

        Assert.Equal(IpcResponseStatus.Ok, response.Status);
        var invocation = Assert.Single(startOperation.Invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(600), invocation.RemainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUtcClockMovesBackward_CapsExecutionWithRemainingSnapshot ()
    {
        var timeProvider = new ManualTimeProvider();
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.ShiftUtc(TimeSpan.FromDays(-1));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: deadlineUtc,
                requestDeadlineRemainingMilliseconds: 700));

        Assert.Equal(IpcResponseStatus.Ok, response.Status);
        Assert.Equal(TimeSpan.FromMilliseconds(700), Assert.Single(startOperation.Invocations).RemainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_AfterDeadlineAdmission_UsesMonotonicRemainingTimeWhenUtcClockMovesBackward ()
    {
        var timeProvider = new DeadlineObservationTransitionTimeProvider(
            monotonicAdvance: TimeSpan.FromMilliseconds(400),
            utcShift: TimeSpan.FromDays(-1));
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
        timeProvider.ArmForRequestDeadlineObservation();
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var frames = await SendStreamingRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Stream),
                requestDeadlineUtc: timeProvider.GetUtcNow().AddSeconds(1),
                requestDeadlineRemainingMilliseconds: 700));

        Assert.Equal(IpcStreamFrameKind.Terminal, Assert.Single(frames).Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(300), Assert.Single(startOperation.Invocations).RemainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUtcClockMovesPastDeadline_ReturnsStructuredTimeoutWithoutDispatch ()
    {
        var timeProvider = new ManualTimeProvider();
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.ShiftUtc(TimeSpan.FromDays(1));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: deadlineUtc,
                requestDeadlineRemainingMilliseconds: 700));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, Assert.Single(response.Errors).Code);
        Assert.Empty(startOperation.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEditorModeIsSpecified_PassesTypedValuesToStartOperation ()
    {
        var lifecycleObservation = IpcUnityEditorObservationTestFactory.Create(IpcEditorLifecycleState.Compiling);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(
                DaemonSessionTestFactory.Create(
                    sessionToken: "session-token",
                    issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
                    endpointTransportKind: IpcTransportKind.UnixDomainSocket,
                    endpointAddress: "/tmp/ucli.sock",
                    processId: 42,
                    ownerProcessId: 24),
                lifecycleObservation),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: DaemonEditorMode.Gui,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate)),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.True(
            response.Status == IpcResponseStatus.Ok,
            string.Join(Environment.NewLine, response.Errors.Select(error => $"{error.Code.Value}: {error.Message}")));
        DaemonStartOperationAssert.EnsureRunningRequested(
            startOperation,
            runtimeContext.StorageRoot,
            unityProjectRoot,
            projectFingerprint,
            TimeSpan.FromMilliseconds(1000),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse payload,
            out _));
        Assert.Equal(lifecycleObservation, payload.LifecycleObservation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenStartOperationAttaches_EmitsAttachedStartStatus ()
    {
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            ownerProcessId: 24,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var lifecycleObservation = IpcUnityEditorObservationTestFactory.Create(IpcEditorLifecycleState.Ready);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.Attached(session, lifecycleObservation),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: DaemonEditorMode.Gui,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(IpcResponseStatus.Ok, response.Status);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse payload,
            out _));
        Assert.Equal(DaemonStartStatus.Attached, payload.StartStatus);
        Assert.Equal(DaemonSessionContractMapper.ToContract(session), payload.Session);
        Assert.Equal(lifecycleObservation, payload.LifecycleObservation);
    }

    private sealed class DeadlineObservationTransitionTimeProvider : TimeProvider
    {
        private readonly ManualTimeProvider inner = new();

        private readonly TimeSpan monotonicAdvance;

        private readonly TimeSpan utcShift;

        private int timestampReadsUntilTransition;

        public DeadlineObservationTransitionTimeProvider (
            TimeSpan monotonicAdvance,
            TimeSpan utcShift)
        {
            if (monotonicAdvance < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(monotonicAdvance), monotonicAdvance, "Monotonic advance must not be negative.");
            }

            this.monotonicAdvance = monotonicAdvance;
            this.utcShift = utcShift;
        }

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow () => inner.GetUtcNow();

        public void ArmForRequestDeadlineObservation ()
        {
            Volatile.Write(ref timestampReadsUntilTransition, 2);
        }

        public override long GetTimestamp ()
        {
            var timestamp = inner.GetTimestamp();
            var readsRemaining = Volatile.Read(ref timestampReadsUntilTransition);
            if (readsRemaining > 0
                && Interlocked.Decrement(ref timestampReadsUntilTransition) == 0)
            {
                inner.Advance(monotonicAdvance);
                inner.ShiftUtc(utcShift);
            }

            return timestamp;
        }

        public override ITimer CreateTimer (
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return inner.CreateTimer(callback, state, dueTime, period);
        }
    }
}
