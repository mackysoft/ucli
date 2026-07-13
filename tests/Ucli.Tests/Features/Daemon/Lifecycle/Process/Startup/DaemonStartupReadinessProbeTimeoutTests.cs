namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using static DaemonStartupReadinessProbeTestSupport;

public sealed class DaemonStartupReadinessProbeTimeoutTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenProjectLockPreflightIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var preflightStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preflightCompletion = new TaskCompletionSource<UnityProjectLockPreflightResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preflightService = new RecordingUnityProjectLockPreflightService
        {
            PrepareAsyncHandler = (_, _) =>
            {
                preflightStarted.TrySetResult();
                return new ValueTask<UnityProjectLockPreflightResult>(preflightCompletion.Task);
            },
        };
        var probe = CreateProbe(
            new RecordingDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused)),
            new UnexpectedUnityLogReader("A timed-out project-lock preflight must not read the Unity log."),
            timeProvider: timeProvider,
            projectLockPreflightService: preflightService);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = probe.WaitUntilReadyAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-preflight-timeout")),
                timeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await preflightStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsReady);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        }
        finally
        {
            preflightCompletion.TrySetResult(UnityProjectLockPreflightResult.Unlocked(
                "/tmp/unity-project/Temp/UnityLockfile"));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenStartupLogReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var logReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logReadCompletion = new TaskCompletionSource<UnityLogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pingClient = new RecordingDaemonPingInfoClient(
            new SocketException((int)SocketError.ConnectionRefused));
        var logReader = new RecordingUnityLogReader
        {
            ReadAsyncHandler = (_, _, _, _) =>
            {
                logReadStarted.TrySetResult();
                return new ValueTask<UnityLogReadResult>(logReadCompletion.Task);
            },
        };
        var probe = CreateProbe(pingClient, logReader, timeProvider: timeProvider);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = probe.WaitUntilReadyAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-log-timeout")),
                timeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await logReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsReady);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        }
        finally
        {
            logReadCompletion.TrySetResult(UnityLogReadResult.Success(
                string.Empty,
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 0));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenNotRunningContinuesWithoutCompilerErrors_ReturnsTimeout ()
    {
        var logReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pingClient = new RecordingDaemonPingInfoClient
        {
            PingAndReadHandler = static (_, _, _, _, _) =>
                ValueTask.FromException<IpcUnityEditorObservation>(new SocketException((int)SocketError.ConnectionRefused)),
        };
        var logReader = new RecordingUnityLogReader
        {
            ReadAsyncHandler = (_, _, _, _) =>
            {
                logReadStarted.TrySetResult();
                return ValueTask.FromResult(UnityLogReadResult.Success(
                    "daemon bootstrap in progress\n",
                    truncated: false,
                    path: "/tmp/unity.log",
                    sizeBytes: 32));
            },
        };
        var timeProvider = new ManualTimeProvider();
        var probe = CreateProbe(pingClient, logReader, timeProvider: timeProvider);
        var timeout = TimeSpan.FromMilliseconds(20);

        var resultTask = probe.WaitUntilReadyAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-readiness-timeout")),
                timeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await pingClient.WaitForFirstInvocationAsync(
            "not-running daemon startup probe",
            TimeSpan.FromSeconds(5));
        await logReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(timeout);
        var result = await TestAwaiter.WaitAsync(
            resultTask,
            "not-running daemon startup timeout",
            TimeSpan.FromSeconds(5));

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
                ValueTask.FromException<IpcUnityEditorObservation>(new TimeoutException("probe timeout")),
        };
        var logReader = new UnexpectedUnityLogReader("Probe timeout should not inspect the Unity log.");
        var timeProvider = new ManualTimeProvider();
        var probe = CreateProbe(pingClient, logReader, timeProvider: timeProvider);

        var result = await WaitUntilStartupDeadlineAsync(
            probe,
            pingClient,
            timeProvider,
            "timed-out daemon startup probe",
            ProjectFingerprintTestFactory.Create("fingerprint-readiness-timeout-exception"));

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }
}
