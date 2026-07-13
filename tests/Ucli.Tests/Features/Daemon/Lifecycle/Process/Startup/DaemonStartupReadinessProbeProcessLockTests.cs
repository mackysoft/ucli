namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Process;
using static DaemonStartupReadinessProbeTestSupport;

public sealed class DaemonStartupReadinessProbeProcessLockTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenLaunchedProcessIsAliveAndProjectLockFileExists_RetriesUntilReady ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            new SocketException((int)SocketError.ConnectionRefused),
            CreatePingPayload());
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = CreateProbe(
            pingClient,
            logReader,
            UnityProjectLockFileProbeResult.Locked("/tmp/unity-project/Temp/UnityLockfile"));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-lock-during-startup");

        var result = await probe.WaitUntilReadyAsync(
            unityProject,
            TimeSpan.FromSeconds(5),
            daemonProcessId: Environment.ProcessId,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Null(result.Error);
        DaemonPingInfoClientAssert.ReadinessProbeRetriedFor(pingClient, unityProject, CancellationToken.None);
        UnityLogReaderAssert.LogInspected(logReader);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenProjectLockFileExistsAfterDaemonIsNotRunning_ReturnsProjectAlreadyOpenImmediately ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused));
        var logReader = new UnexpectedUnityLogReader("Project already open should be reported without Unity log inspection.");
        var probe = CreateProbe(
            pingClient,
            logReader,
            UnityProjectLockFileProbeResult.Locked("/tmp/unity-project/Temp/UnityLockfile"));

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-already-open"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
        Assert.Contains("already open", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonProcessExitedBeforeReady_ReturnsInternalErrorImmediately ()
    {
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = CreateProbe(
            new UnexpectedDaemonPingInfoClient("Exited daemon process must be classified before pinging the daemon endpoint."),
            logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-process-exited"),
            TimeSpan.FromSeconds(5),
            daemonProcessId: int.MaxValue,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("process exited before startup readiness was confirmed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"ProcessId={int.MaxValue}", error.Message, StringComparison.Ordinal);
        UnityLogReaderAssert.LogInspected(logReader);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonProcessExitedAndStaleProjectLockFileExists_PreservesProcessExitFailure ()
    {
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var probe = CreateProbe(
            new UnexpectedDaemonPingInfoClient("Exited daemon process with stale lock must be classified before pinging the daemon endpoint."),
            logReader,
            UnityProjectLockFileProbeResult.Locked("/tmp/unity-project/Temp/UnityLockfile"));

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-exited-lock-file"),
            TimeSpan.FromSeconds(5),
            daemonProcessId: int.MaxValue,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Null(error.Code);
        Assert.Contains("process exited before startup readiness was confirmed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Stale Unity project lock file was removed", error.Message, StringComparison.Ordinal);
        UnityLogReaderAssert.LogInspected(logReader);
    }
}
