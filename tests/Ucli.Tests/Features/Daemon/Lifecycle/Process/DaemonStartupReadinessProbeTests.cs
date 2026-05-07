using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class DaemonStartupReadinessProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingSucceeds_ReturnsReadyWithoutLogInspection ()
    {
        var pingClient = new StubDaemonPingInfoClient(static () => ValueTask.FromResult(CreatePingPayload(canAcceptExecutionRequests: true)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, false, "/tmp/unity.log", 0),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-success"),
            TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Equal(0, logReader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsStarting_RetriesUntilExecutionIsAccepted ()
    {
        var attempt = 0;
        var pingClient = new StubDaemonPingInfoClient(() =>
        {
            attempt++;
            return ValueTask.FromResult(CreatePingPayload(
                lifecycleState: attempt == 1 ? IpcEditorLifecycleStateCodec.Starting : IpcEditorLifecycleStateCodec.Ready,
                canAcceptExecutionRequests: attempt != 1));
        });
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, false, "/tmp/unity.log", 0),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-starting"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(2, pingClient.CallCount);
        Assert.Equal(0, logReader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingReportsDomainReloading_RetriesUntilExecutionIsAccepted ()
    {
        var attempt = 0;
        var pingClient = new StubDaemonPingInfoClient(() =>
        {
            attempt++;
            return ValueTask.FromResult(CreatePingPayload(
                lifecycleState: attempt == 1 ? IpcEditorLifecycleStateCodec.DomainReloading : IpcEditorLifecycleStateCodec.Ready,
                canAcceptExecutionRequests: attempt != 1));
        });
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, false, "/tmp/unity.log", 0),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-domain-reloading"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        Assert.Equal(2, pingClient.CallCount);
        Assert.Equal(0, logReader.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcEditorLifecycleStateCodec.Playmode, IpcEditorBlockingReasonCodec.PlayMode, "Exit Play Mode and retry after lifecycleState=ready.")]
    [InlineData(IpcEditorLifecycleStateCodec.BlockedByModal, IpcEditorBlockingReasonCodec.ModalDialog, "Resolve the modal dialog and retry after lifecycleState=ready.")]
    [InlineData(IpcEditorLifecycleStateCodec.SafeMode, IpcEditorBlockingReasonCodec.SafeMode, "Resolve compiler errors and retry after lifecycleState=ready.")]
    [InlineData(IpcEditorLifecycleStateCodec.ShuttingDown, IpcEditorBlockingReasonCodec.Shutdown, "Start a new daemon after shutdown finishes.")]
    public async Task WaitUntilReady_WhenPingReportsNonWaitableLifecycleState_ReturnsInternalErrorImmediately (
        string lifecycleState,
        string blockingReason,
        string expectedMessageSuffix)
    {
        var pingClient = new StubDaemonPingInfoClient(staticLifecycleState: lifecycleState, staticBlockingReason: blockingReason);
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, false, "/tmp/unity.log", 0),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext($"fingerprint-readiness-{lifecycleState}"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains($"lifecycleState={lifecycleState}", error.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessageSuffix, error.Message, StringComparison.Ordinal);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Equal(0, logReader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonLogContainsCompilerErrorMarker_ReturnsInternalErrorImmediately ()
    {
        var pingClient = new StubDaemonPingInfoClient(() => ValueTask.FromException<IpcPingResponse>(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Aborting batchmode due to failure:\nScripts have compiler errors.\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 128),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-compiler-marker"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("scripts have compiler errors", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Marker=Scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Equal(1, logReader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonLogContainsCompilerErrorCode_ReturnsInternalErrorWithFirstErrorLine ()
    {
        var pingClient = new StubDaemonPingInfoClient(() => ValueTask.FromException<IpcPingResponse>(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Assets/Foo.cs(10,1): error CS0246: MissingType\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 64),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-compiler-cs"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("FirstError=Assets/Foo.cs(10,1): error CS0246: MissingType", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonLogContainsPackageResolutionFailure_ReturnsInternalErrorImmediately ()
    {
        var pingClient = new StubDaemonPingInfoClient(() => ValueTask.FromException<IpcPingResponse>(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                """
                An error occurred while resolving packages:
                  Project has invalid dependencies:
                    com.unity.test-framework: Package [com.unity.test-framework@1.6.0] cannot be found
                """,
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 256),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-package-error"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("package resolution failed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FirstError=com.unity.test-framework: Package [com.unity.test-framework@1.6.0] cannot be found", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Equal(1, logReader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonLogContainsProjectAlreadyOpenMarker_ReturnsProjectAlreadyOpenImmediately ()
    {
        var pingClient = new StubDaemonPingInfoClient(() => ValueTask.FromException<IpcPingResponse>(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                """
                COMMAND LINE ARGUMENTS:
                -projectPath
                /tmp/unity-project
                It looks like another Unity instance is running with this project open.
                """,
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 256),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-already-open"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
        Assert.Contains("already open", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Equal(1, logReader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenOnlyPreviousSessionHasPackageResolutionFailure_ReturnsTimeout ()
    {
        var pingClient = new StubDaemonPingInfoClient(() => ValueTask.FromException<IpcPingResponse>(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                """
                COMMAND LINE ARGUMENTS:
                -projectPath
                /tmp/old
                An error occurred while resolving packages:
                  Project has invalid dependencies:
                    com.unity.modules.adaptiveperformance: Package [com.unity.modules.adaptiveperformance@1.0.0] cannot be found
                COMMAND LINE ARGUMENTS:
                -projectPath
                /tmp/new
                [Package Manager] Done resolving packages in 1.00 seconds
                """,
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 512),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-ignore-previous-session-errors"),
            TimeSpan.FromMilliseconds(20),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.True(logReader.CallCount >= 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonProcessExitedBeforeReady_ReturnsInternalErrorImmediately ()
    {
        var pingClient = new StubDaemonPingInfoClient(static () => ValueTask.FromResult(CreatePingPayload(canAcceptExecutionRequests: true)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-process-exited"),
            TimeSpan.FromSeconds(5),
            daemonProcessId: int.MaxValue,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("process exited before startup readiness was confirmed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"ProcessId={int.MaxValue}", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, pingClient.CallCount);
        Assert.Equal(1, logReader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenNotRunningContinuesWithoutCompilerErrors_ReturnsTimeout ()
    {
        var pingClient = new StubDaemonPingInfoClient(() => ValueTask.FromException<IpcPingResponse>(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-timeout"),
            TimeSpan.FromMilliseconds(20),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.True(logReader.CallCount >= 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingTimesOutUntilDeadline_ReturnsTimeout ()
    {
        var pingClient = new StubDaemonPingInfoClient(() => ValueTask.FromException<IpcPingResponse>(new TimeoutException("probe timeout")));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReady(
            CreateContext("fingerprint-readiness-timeout-exception"),
            TimeSpan.FromMilliseconds(20),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(0, logReader.CallCount);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonStartupReadinessProbe CreateProbe (
        StubDaemonPingInfoClient pingClient,
        StubUnityLogReader logReader)
    {
        return new DaemonStartupReadinessProbe(pingClient, logReader);
    }

    private static IpcPingResponse CreatePingPayload (
        string lifecycleState = IpcEditorLifecycleStateCodec.Ready,
        bool canAcceptExecutionRequests = true)
    {
        return new IpcPingResponse(
            ServerVersion: "1.0.0",
            Runtime: IpcEditorRuntimeCodec.Batchmode,
            UnityVersion: "2023.2.22f1",
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: lifecycleState,
            BlockingReason: canAcceptExecutionRequests
                ? null
                : lifecycleState switch
                {
                    IpcEditorLifecycleStateCodec.Starting => IpcEditorBlockingReasonCodec.Startup,
                    IpcEditorLifecycleStateCodec.Busy => IpcEditorBlockingReasonCodec.Busy,
                    IpcEditorLifecycleStateCodec.Compiling => IpcEditorBlockingReasonCodec.Compile,
                    IpcEditorLifecycleStateCodec.DomainReloading => IpcEditorBlockingReasonCodec.DomainReload,
                    IpcEditorLifecycleStateCodec.ShuttingDown => IpcEditorBlockingReasonCodec.Shutdown,
                    _ => null,
                },
            CompileGeneration: "0",
            DomainReloadGeneration: "0",
            CanAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    private sealed class StubDaemonPingInfoClient : IDaemonPingInfoClient
    {
        private readonly Func<ValueTask<IpcPingResponse>> handler;

        public StubDaemonPingInfoClient (Func<ValueTask<IpcPingResponse>> handler)
        {
            this.handler = handler;
        }

        public StubDaemonPingInfoClient (
            string staticLifecycleState,
            string? staticBlockingReason)
            : this(() => ValueTask.FromResult(new IpcPingResponse(
                ServerVersion: "1.0.0",
                Runtime: IpcEditorRuntimeCodec.Batchmode,
                UnityVersion: "2023.2.22f1",
                CompileState: IpcCompileStateCodec.Ready,
                LifecycleState: staticLifecycleState,
                BlockingReason: staticBlockingReason,
                CompileGeneration: "0",
                DomainReloadGeneration: "0",
                CanAcceptExecutionRequests: false)))
        {
        }

        public int CallCount { get; private set; }

        public ValueTask<IpcPingResponse> PingAndRead (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return handler();
        }
    }

    private sealed class StubUnityLogReader : IUnityLogReader
    {
        public UnityLogReadResult NextResult { get; set; } = UnityLogReadResult.Success(string.Empty, false, "/tmp/unity.log", 0);

        public int CallCount { get; private set; }

        public ValueTask<UnityLogReadResult> ReadTail (
            string storageRoot,
            string projectFingerprint,
            int maxBytes = UnityLogReader.DefaultMaxBytes,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }
}
