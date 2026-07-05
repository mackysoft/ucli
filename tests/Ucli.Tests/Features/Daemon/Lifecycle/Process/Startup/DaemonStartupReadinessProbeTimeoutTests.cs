namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using static DaemonStartupReadinessProbeTestSupport;

public sealed class DaemonStartupReadinessProbeTimeoutTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenNotRunningContinuesWithoutCompilerErrors_ReturnsTimeout ()
    {
        var pingClient = new RecordingDaemonPingInfoClient
        {
            PingAndReadHandler = static (_, _, _, _, _) =>
                ValueTask.FromException<IpcPingResponse>(new SocketException((int)SocketError.ConnectionRefused)),
        };
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "daemon bootstrap in progress\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 32),
        };
        var timeProvider = new ManualTimeProvider();
        var probe = CreateProbe(pingClient, logReader, timeProvider: timeProvider);

        var result = await WaitUntilStartupDeadlineAsync(
            probe,
            pingClient,
            timeProvider,
            "not-running daemon startup probe",
            "fingerprint-readiness-timeout");

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        UnityLogReaderAssert.LogInspected(logReader);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenPingTimesOutUntilDeadline_ReturnsTimeout ()
    {
        var pingClient = new RecordingDaemonPingInfoClient
        {
            PingAndReadHandler = static (_, _, _, _, _) =>
                ValueTask.FromException<IpcPingResponse>(new TimeoutException("probe timeout")),
        };
        var logReader = new UnexpectedUnityLogReader("Probe timeout should not inspect the Unity log.");
        var timeProvider = new ManualTimeProvider();
        var probe = CreateProbe(pingClient, logReader, timeProvider: timeProvider);

        var result = await WaitUntilStartupDeadlineAsync(
            probe,
            pingClient,
            timeProvider,
            "timed-out daemon startup probe",
            "fingerprint-readiness-timeout-exception");

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }
}
