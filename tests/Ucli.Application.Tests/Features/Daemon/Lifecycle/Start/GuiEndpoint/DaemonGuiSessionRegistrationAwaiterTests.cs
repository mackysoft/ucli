using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiSessionRegistrationAwaiterTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenSessionAndPingMatch_ReturnsSuccess ()
    {
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var session = CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingResponse(unityProject.ProjectFingerprint, "gui"));
        var awaiter = CreateAwaiter(sessionStore, pingClient);

        var result = await awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        DaemonPingInfoClientAssert.GuiSessionPingRead(pingClient, unityProject, session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenSessionReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompletion = new TaskCompletionSource<DaemonSessionReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, cancellationToken) =>
            {
                _ = cancellationToken.UnsafeRegister(
                    static state => ((TaskCompletionSource)state!).TrySetResult(),
                    readCancellationObserved);
                readStarted.TrySetResult();
                try
                {
                    return await readCompletion.Task.ConfigureAwait(false);
                }
                finally
                {
                    readFinished.TrySetResult();
                }
            },
        };
        var pingClient = new UnexpectedDaemonPingInfoClient("A timed-out session read must not reach the GUI daemon ping.");
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(
                unityProject,
                expectedProcessId: 4321,
                WaitTimeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(readStarted.Task, "Non-cooperative GUI session read", SignalWaitTimeout);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(WaitTimeout),
            "GUI session read deadline timer",
            SignalWaitTimeout);

        try
        {
            timeProvider.Advance(WaitTimeout);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "GUI session read deadline result",
                SignalWaitTimeout);
            await TestAwaiter.WaitAsync(
                readCancellationObserved.Task,
                "GUI session read cancellation",
                SignalWaitTimeout);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error.Code);
        }
        finally
        {
            readCompletion.TrySetResult(DaemonSessionReadResult.Success(null));
            await TestAwaiter.WaitAsync(readFinished.Task, "GUI session read completion", SignalWaitTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenTimeoutExceedsProbeCap_CapsPingAttempt ()
    {
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(5000).Context.UnityProject;
        var session = CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingResponse(unityProject.ProjectFingerprint, "gui"));
        var awaiter = CreateAwaiter(sessionStore, pingClient);

        var result = await awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        DaemonPingInfoClientAssert.GuiSessionPingReadWithAttemptCap(pingClient, unityProject, session);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(9876, "fingerprint", "gui")]
    [InlineData(4321, "other-fingerprint", "gui")]
    [InlineData(4321, "fingerprint", "batchmode")]
    public async Task WaitForSession_WhenStoredSessionDoesNotMatchExpectedContract_DoesNotProbeAndTimesOut (
        int storedProcessId,
        string storedProjectFingerprint,
        string storedEditorMode)
    {
        var timeProvider = new ManualTimeProvider();
        var firstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(CreateGuiSession(
            storedProjectFingerprint,
            storedProcessId,
            storedEditorMode)))
        {
            OnRead = () => firstRead.TrySetResult(),
        };
        var pingClient = new UnexpectedDaemonPingInfoClient("Stored GUI session mismatch should not be pinged.");
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(firstRead.Task, "first session read", TimeSpan.FromSeconds(5));
        await timeProvider.WaitForTimerDueWithinAsync(WaitTimeout).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(WaitTimeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "session mismatch timeout", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenStoredProcessStartTimeDiffersWithinTolerance_ProbesAndReturnsSuccess ()
    {
        var expectedProcessStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var session = CreateGuiSession(
            unityProject.ProjectFingerprint,
            processId: 4321,
            processStartedAtUtc: expectedProcessStartedAtUtc.AddMilliseconds(1));
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingResponse(unityProject.ProjectFingerprint, "gui"));
        var awaiter = CreateAwaiter(sessionStore, pingClient);

        var result = await awaiter.WaitForSessionAsync(
            unityProject,
            expectedProcessId: 4321,
            WaitTimeout,
            expectedProcessStartedAtUtc: expectedProcessStartedAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        DaemonPingInfoClientAssert.GuiSessionPingRead(pingClient, unityProject, session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenStoredProcessStartTimeExceedsTolerance_DoesNotProbeAndTimesOut ()
    {
        var timeProvider = new ManualTimeProvider();
        var firstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedProcessStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(CreateGuiSession(
            unityProject.ProjectFingerprint,
            processId: 4321,
            processStartedAtUtc: expectedProcessStartedAtUtc.Add(DaemonProcessStartTimeMatcher.Tolerance).AddMilliseconds(1))))
        {
            OnRead = () => firstRead.TrySetResult(),
        };
        var pingClient = new UnexpectedDaemonPingInfoClient("Stored process start-time mismatch should not be pinged.");
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(
            unityProject,
            expectedProcessId: 4321,
            WaitTimeout,
            expectedProcessStartedAtUtc: expectedProcessStartedAtUtc).AsTask();
        await TestAwaiter.WaitAsync(firstRead.Task, "first session read", TimeSpan.FromSeconds(5));
        timeProvider.Advance(WaitTimeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "session start-time mismatch timeout", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenPingProjectFingerprintDiffers_DoesNotAttach ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var session = CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingResponse("other-fingerprint", "gui"))
        {
            OnPingAndRead = () => pingObserved.TrySetResult(),
        };
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(pingObserved.Task, "first ping", TimeSpan.FromSeconds(5));
        timeProvider.Advance(WaitTimeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "ping mismatch timeout", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        DaemonPingInfoClientAssert.GuiSessionPingAttempted(pingClient, unityProject, session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenPingTimesOut_RetriesUntilOverallTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321)));
        var pingClient = new RecordingDaemonPingInfoClient(new TimeoutException("probe timed out"))
        {
            OnPingAndRead = () => pingObserved.TrySetResult(),
        };
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(pingObserved.Task, "first timeout ping", TimeSpan.FromSeconds(5));
        timeProvider.Advance(WaitTimeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "ping timeout result", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenPingTimesOut_RereadsSessionBeforeNextPing ()
    {
        var timeProvider = new ManualTimeProvider();
        var firstPingObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(5000).Context.UnityProject;
        var firstSession = CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321) with
        {
            SessionToken = "first-session-token",
        };
        var replacementSession = firstSession with
        {
            SessionToken = "replacement-session-token",
        };
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResult.Success(firstSession)
                : DaemonSessionReadResult.Success(replacementSession),
        };
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("old endpoint timed out"),
            CreatePingResponse(unityProject.ProjectFingerprint, "gui"))
        {
            OnPingAndRead = () => firstPingObserved.TrySetResult(),
        };
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(
            unityProject,
            expectedProcessId: 4321,
            TimeSpan.FromSeconds(5)).AsTask();
        await TestAwaiter.WaitAsync(firstPingObserved.Task, "first registration ping", TimeSpan.FromSeconds(5));
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(retryDelay),
            "Registration retry timer",
            SignalWaitTimeout);
        timeProvider.Advance(retryDelay);

        var result = await TestAwaiter.WaitAsync(resultTask, "replacement registration", TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(replacementSession, result.Session);
        Assert.Collection(
            pingClient.Invocations,
            invocation => Assert.Equal(firstSession.SessionToken, invocation.SessionToken),
            invocation => Assert.Equal(replacementSession.SessionToken, invocation.SessionToken));
        Assert.Equal(2, sessionStore.ReadInvocations.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenPingThrowsReachabilityError_RetriesUntilOverallTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321)));
        var pingClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("not running"))
        {
            OnPingAndRead = () => pingObserved.TrySetResult(),
        };
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(pingObserved.Task, "first reachability ping", TimeSpan.FromSeconds(5));
        timeProvider.Advance(WaitTimeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "reachability timeout result", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenPingThrowsUnexpectedError_ReturnsInternalError ()
    {
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321)));
        var pingClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("unexpected"));
        var awaiter = CreateAwaiter(
            sessionStore,
            pingClient,
            reachabilityClassifier: new StubDaemonReachabilityClassifier(_ => false));

        var result = await awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("unexpected", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenFirstReadIsInvalidSession_RetriesUntilMatchingSessionAppears ()
    {
        var timeProvider = new ManualTimeProvider();
        var firstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstInvalidSessionReturned = false;
        var unityProject = DaemonCommandExecutionContextTestFactory.Create(1000).Context.UnityProject;
        var session = CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null))
        {
            ReadHandler = _ =>
            {
                if (!firstInvalidSessionReturned)
                {
                    firstInvalidSessionReturned = true;
                    firstRead.TrySetResult();
                    return DaemonSessionReadResult.Failure(
                        ExecutionError.InvalidArgument("invalid session"),
                        DaemonSessionReadFailureKind.InvalidSession,
                        session: null,
                        artifactIdentity: null);
                }

                return DaemonSessionReadResult.Success(session);
            },
        };
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingResponse(unityProject.ProjectFingerprint, "gui"));
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(firstRead.Task, "first invalid session read", TimeSpan.FromSeconds(5));
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(retryDelay),
            "Invalid session retry timer",
            SignalWaitTimeout);
        timeProvider.Advance(retryDelay);

        var result = await TestAwaiter.WaitAsync(resultTask, "retry success", TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
    }

    private static DaemonGuiSessionRegistrationAwaiter CreateAwaiter (
        IDaemonSessionStore sessionStore,
        IDaemonPingInfoClient pingClient,
        TimeProvider? timeProvider = null,
        IDaemonReachabilityClassifier? reachabilityClassifier = null)
    {
        return new DaemonGuiSessionRegistrationAwaiter(
            sessionStore,
            pingClient,
            reachabilityClassifier ?? new StubDaemonReachabilityClassifier(_ => true),
            timeProvider ?? new ManualTimeProvider());
    }

    private static DaemonSession CreateGuiSession (
        string projectFingerprint,
        int processId,
        string editorMode = "gui",
        DateTimeOffset? processStartedAtUtc = null)
    {
        return DaemonSessionTestFactory.Create(
            projectFingerprint: projectFingerprint,
            processId: processId,
            editorMode: editorMode,
            processStartedAtUtc: processStartedAtUtc);
    }

    private static IpcPingResponse CreatePingResponse (
        string projectFingerprint,
        string editorMode)
    {
        return new IpcPingResponse(
            ServerVersion: "0.0.1",
            EditorMode: editorMode,
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: projectFingerprint,
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: IpcEditorLifecycleStateCodec.Ready,
            BlockingReason: null,
            CompileGeneration: "1",
            DomainReloadGeneration: "1",
            CanAcceptExecutionRequests: true);
    }
}
