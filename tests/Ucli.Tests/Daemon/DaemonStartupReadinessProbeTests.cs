namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonStartupReadinessProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingSucceeds_ReturnsReadyWithoutLogInspection ()
    {
        var pingClient = new StubDaemonPingClient(static () => ValueTask.CompletedTask);
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, false, "/tmp/unity.log", 0),
        };
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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
    public async Task WaitUntilReady_WhenDaemonLogContainsCompilerErrorMarker_ReturnsInternalErrorImmediately ()
    {
        var pingClient = new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Aborting batchmode due to failure:\nScripts have compiler errors.\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 128),
        };
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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
        var pingClient = new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Assets/Foo.cs(10,1): error CS0246: MissingType\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 64),
        };
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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
        var pingClient = new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused)));
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
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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
    public async Task WaitUntilReady_WhenOnlyPreviousSessionHasPackageResolutionFailure_ReturnsTimeout ()
    {
        var pingClient = new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused)));
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
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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
        var pingClient = new StubDaemonPingClient(static () => ValueTask.CompletedTask);
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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
        var pingClient = new StubDaemonPingClient(() => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused)));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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
        var pingClient = new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("probe timeout")));
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = new DaemonStartupReadinessProbe(pingClient, logReader);

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

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<ValueTask> handler;

        public StubDaemonPingClient (Func<ValueTask> handler)
        {
            this.handler = handler;
        }

        public int CallCount { get; private set; }

        public ValueTask Ping (
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
