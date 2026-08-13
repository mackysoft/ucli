namespace MackySoft.Ucli.Tests;

using System.IO.Pipes;

public sealed class ProcessRunnerTests
{
    private const int ControlledOutputLength = 5000;

    private const char ControlledOutputCharacter = '0';

    private static readonly TimeSpan InheritedOutputHandleLifetime = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan RequiredOutputCompletionHandleLifetime = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan RequiredOutputCompletionTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan NonResponsiveProcessStartupTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan NonResponsiveProcessGraceTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly TimeSpan NonResponsiveProcessForceKillWaitTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ControlledOutputWatchdogTimeout = TimeSpan.FromSeconds(30);

    private const byte ReadySignal = 1;

    private const byte ReleaseSignal = 2;

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WithInvalidExecutable_ReturnsStartFailed ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            new ProcessRunRequest(
                FileName: "__ucli_missing_executable__",
                Arguments: Array.Empty<string>(),
                Timeout: TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        Assert.Equal(ProcessRunStatus.StartFailed, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenCaptureStandardOutputIsEnabled_PreservesFullOutput ()
    {
        var result = await RunControlledStandardOutputAsync(captureStandardOutput: true);

        Assert.Equal(ProcessRunStatus.Exited, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            new string(ControlledOutputCharacter, ControlledOutputLength)
            + Environment.NewLine,
            result.StandardOutput);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenCaptureStandardOutputIsDisabled_DoesNotPreserveOutput ()
    {
        var result = await RunControlledStandardOutputAsync(captureStandardOutput: false);

        Assert.Equal(ProcessRunStatus.Exited, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.StandardOutput);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenCallerCancellationRacesTimeout_PrefersCanceledResult ()
    {
        var runner = new ProcessRunner();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await runner.RunAsync(
                CreateLongRunningRequest(TimeSpan.Zero),
                cancellationTokenSource.Token).WaitAsync(SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.Canceled, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenProcessExceedsTimeout_ReturnsTimedOut ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
                CreateLongRunningRequest(TimeSpan.FromMilliseconds(20)),
                CancellationToken.None).WaitAsync(SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Null(result.ExitCode);
        Assert.Equal(ProcessTerminationResult.ForceKilled, result.TerminationResult);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WithGracefulThenKill_WhenProcessHandlesGracefulExit_ReturnsGracefulExited ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("process-runner", "graceful-termination");
        var markerPath = scope.GetPath("term-marker");
        var invocation = TestProcessInvocations.CreateUnixTermSignalMarkerLoop(markerPath);
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
                new ProcessRunRequest(
                    FileName: invocation.FileName,
                    Arguments: invocation.Arguments,
                    Timeout: TimeSpan.FromMilliseconds(50),
                    OutputDrainMode: ProcessOutputDrainMode.BestEffort,
                    TerminationPolicy: new ProcessTerminationPolicy(
                        ProcessTerminationMode.GracefulThenKill,
                        TimeSpan.FromMilliseconds(250),
                        TimeSpan.FromMilliseconds(250))),
                CancellationToken.None).WaitAsync(SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Equal(ProcessTerminationResult.GracefulExited, result.TerminationResult);
        Assert.True(File.Exists(markerPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WithGracefulThenKill_WhenProcessDoesNotExitGracefully_ReturnsForceKilled ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
                CreateGracefulThenKillNonResponsiveRequest(),
                CancellationToken.None).WaitAsync(SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Equal(ProcessTerminationResult.ForceKilled, result.TerminationResult);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenOutputDrainModeIsBestEffortAndDescendantKeepsOutputOpen_ReturnsAfterParentExit ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
                CreateExitedProcessWithInheritedOutputHandleRequest(
                    timeout: TimeSpan.FromMilliseconds(250),
                    outputDrainMode: ProcessOutputDrainMode.BestEffort,
                    childLifetime: InheritedOutputHandleLifetime),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(8));

        Assert.Equal(ProcessRunStatus.Exited, result.Status);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenOutputCompletionIsRequiredAndDescendantKeepsOutputOpen_ReturnsTimedOut ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
                CreateExitedProcessWithInheritedOutputHandleRequest(
                    timeout: RequiredOutputCompletionTimeout,
                    outputDrainMode: ProcessOutputDrainMode.WaitForCompletion,
                    childLifetime: RequiredOutputCompletionHandleLifetime),
                CancellationToken.None).WaitAsync(SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    private static ProcessRunRequest CreateLongRunningRequest (
        TimeSpan timeout,
        ProcessOutputDrainMode outputDrainMode = ProcessOutputDrainMode.WaitForCompletion)
    {
        var invocation = TestProcessInvocations.CreateLongRunning();

        return new ProcessRunRequest(
            FileName: invocation.FileName,
            Arguments: invocation.Arguments,
            Timeout: timeout,
            OutputDrainMode: outputDrainMode);
    }

    private static ProcessRunRequest CreateGracefulThenKillNonResponsiveRequest ()
    {
        var terminationPolicy = new ProcessTerminationPolicy(
            ProcessTerminationMode.GracefulThenKill,
            NonResponsiveProcessGraceTimeout,
            NonResponsiveProcessForceKillWaitTimeout);
        var invocation = OperatingSystem.IsWindows()
            ? TestProcessInvocations.CreateLongRunning()
            : TestProcessInvocations.CreateUnixTermSignalIgnoredLoop();
        return new ProcessRunRequest(
            FileName: invocation.FileName,
            Arguments: invocation.Arguments,
            Timeout: NonResponsiveProcessStartupTimeout,
            OutputDrainMode: ProcessOutputDrainMode.BestEffort,
            TerminationPolicy: terminationPolicy);
    }

    private static ProcessRunRequest CreateExitedProcessWithInheritedOutputHandleRequest (
        TimeSpan timeout,
        ProcessOutputDrainMode outputDrainMode,
        TimeSpan childLifetime)
    {
        var invocation = TestProcessInvocations.CreateUnixExitedParentWithInheritedOutputHandle(childLifetime);

        return new ProcessRunRequest(
            FileName: invocation.FileName,
            Arguments: invocation.Arguments,
            Timeout: timeout,
            OutputDrainMode: outputDrainMode);
    }

    private static async Task<ProcessRunResult> RunControlledStandardOutputAsync (bool captureStandardOutput)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        using var watchdogCancellationTokenSource = new CancellationTokenSource(ControlledOutputWatchdogTimeout);
        using var watchdogRegistration = watchdogCancellationTokenSource.Token.Register(cancellationTokenSource.Cancel);
        var pipeName = "ucli-" + Guid.NewGuid().ToString("N")[..16];
        var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        var invocation = TestProcessInvocations.CreateControlledStandardOutput(pipeName);
        var runner = new ProcessRunner();
        Task<ProcessRunResult>? runTask = null;

        try
        {
            runTask = runner.RunAsync(
                new ProcessRunRequest(
                    FileName: invocation.FileName,
                    Arguments: invocation.Arguments,
                    Timeout: Timeout.InfiniteTimeSpan,
                    CaptureStandardOutput: captureStandardOutput,
                    OutputDrainMode: ProcessOutputDrainMode.WaitForCompletion),
                cancellationTokenSource.Token);

            await pipe.WaitForConnectionAsync(cancellationTokenSource.Token);
            var readyBuffer = new byte[1];
            Assert.Equal(1, await pipe.ReadAsync(readyBuffer, cancellationTokenSource.Token));
            Assert.Equal(ReadySignal, readyBuffer[0]);
            await pipe.WriteAsync(new byte[] { ReleaseSignal }, cancellationTokenSource.Token);
            await pipe.FlushAsync(cancellationTokenSource.Token);

            return await runTask;
        }
        finally
        {
            cancellationTokenSource.Cancel();
            await pipe.DisposeAsync();
            if (runTask is not null)
            {
                await runTask;
            }
        }
    }
}
