using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiSessionRegistrationAwaiterTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenSessionAndPingMatch_ReturnsSuccess ()
    {
        var unityProject = DaemonServiceTestContext.CreateExecutionContext(1000).Context.UnityProject;
        var session = CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(session),
        };
        var pingClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Response = CreatePingResponse(unityProject.ProjectFingerprint, DaemonEditorModeValues.Gui),
        };
        var awaiter = CreateAwaiter(sessionStore, pingClient);

        var result = await awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Equal(session.SessionToken, pingClient.LastSessionToken);
        Assert.False(pingClient.LastValidateProjectFingerprint);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(9876, "fingerprint", DaemonEditorModeValues.Gui)]
    [InlineData(4321, "other-fingerprint", DaemonEditorModeValues.Gui)]
    [InlineData(4321, "fingerprint", DaemonEditorModeValues.Batchmode)]
    public async Task WaitForSession_WhenStoredSessionDoesNotMatchExpectedContract_DoesNotProbeAndTimesOut (
        int storedProcessId,
        string storedProjectFingerprint,
        string storedEditorMode)
    {
        var timeProvider = new ManualTimeProvider();
        var firstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonServiceTestContext.CreateExecutionContext(1000).Context.UnityProject;
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateGuiSession(
                storedProjectFingerprint,
                storedProcessId,
                storedEditorMode)),
            OnRead = () => firstRead.TrySetResult(),
        };
        var pingClient = new DaemonServiceTestContext.StubDaemonPingInfoClient();
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(firstRead.Task, "first session read", TimeSpan.FromSeconds(5));
        timeProvider.Advance(WaitTimeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "session mismatch timeout", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Equal(0, pingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenPingProjectFingerprintDiffers_DoesNotAttach ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonServiceTestContext.CreateExecutionContext(1000).Context.UnityProject;
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321)),
        };
        var pingClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Response = CreatePingResponse("other-fingerprint", DaemonEditorModeValues.Gui),
            OnPingAndRead = () => pingObserved.TrySetResult(),
        };
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(pingObserved.Task, "first ping", TimeSpan.FromSeconds(5));
        timeProvider.Advance(WaitTimeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "ping mismatch timeout", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.True(pingClient.CallCount >= 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForSession_WhenPingTimesOut_RetriesUntilOverallTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonServiceTestContext.CreateExecutionContext(1000).Context.UnityProject;
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321)),
        };
        var pingClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new TimeoutException("probe timed out"),
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
    public async Task WaitForSession_WhenPingThrowsReachabilityError_RetriesUntilOverallTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unityProject = DaemonServiceTestContext.CreateExecutionContext(1000).Context.UnityProject;
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321)),
        };
        var pingClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("not running"),
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
        var unityProject = DaemonServiceTestContext.CreateExecutionContext(1000).Context.UnityProject;
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321)),
        };
        var pingClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("unexpected"),
        };
        var awaiter = CreateAwaiter(
            sessionStore,
            pingClient,
            reachabilityClassifier: new DaemonServiceTestContext.StubDaemonReachabilityClassifier(_ => false));

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
        var unityProject = DaemonServiceTestContext.CreateExecutionContext(1000).Context.UnityProject;
        var session = CreateGuiSession(unityProject.ProjectFingerprint, processId: 4321);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadHandler = readCount =>
            {
                if (readCount == 1)
                {
                    firstRead.TrySetResult();
                    return DaemonSessionReadResult.Failure(
                        ExecutionError.InvalidArgument("invalid session"),
                        DaemonSessionReadFailureKind.InvalidSession);
                }

                return DaemonSessionReadResult.Success(session);
            },
        };
        var pingClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Response = CreatePingResponse(unityProject.ProjectFingerprint, DaemonEditorModeValues.Gui),
        };
        var awaiter = CreateAwaiter(sessionStore, pingClient, timeProvider);

        var resultTask = awaiter.WaitForSessionAsync(unityProject, expectedProcessId: 4321, WaitTimeout).AsTask();
        await TestAwaiter.WaitAsync(firstRead.Task, "first invalid session read", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        var result = await TestAwaiter.WaitAsync(resultTask, "retry success", TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        Assert.Equal(2, sessionStore.ReadCallCount);
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
            reachabilityClassifier ?? new DaemonServiceTestContext.StubDaemonReachabilityClassifier(_ => true),
            timeProvider);
    }

    private static DaemonSession CreateGuiSession (
        string projectFingerprint,
        int processId,
        string editorMode = DaemonEditorModeValues.Gui)
    {
        return DaemonServiceTestContext.CreateSession() with
        {
            ProjectFingerprint = projectFingerprint,
            ProcessId = processId,
            EditorMode = editorMode,
        };
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

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSessionReadResult ReadResult { get; set; } = DaemonSessionReadResult.Success(null);

        public Func<int, DaemonSessionReadResult>? ReadHandler { get; set; }

        public Action? OnRead { get; set; }

        public int ReadCallCount { get; private set; }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            OnRead?.Invoke();
            return ValueTask.FromResult(ReadHandler?.Invoke(ReadCallCount) ?? ReadResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }
}
