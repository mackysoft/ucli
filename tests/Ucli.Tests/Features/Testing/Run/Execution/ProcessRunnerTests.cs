using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

public sealed class ProcessRunnerTests
{
    private const int LongOutputLength = 5000;

    private const char LongOutputCharacter = '0';

    private static readonly TimeSpan InheritedOutputHandleLifetime = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan NonResponsiveProcessStartupTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan NonResponsiveProcessGraceTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly TimeSpan NonResponsiveProcessForceKillWaitTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

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
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            CreateLongOutputRequest(captureStandardOutput: true),
            CancellationToken.None);

        Assert.Equal(ProcessRunStatus.Exited, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.StandardOutput);
        Assert.Equal(LongOutputLength, result.StandardOutput!.TrimEnd('\r', '\n').Length);
        Assert.Equal(new string(LongOutputCharacter, LongOutputLength), result.StandardOutput.TrimEnd('\r', '\n'));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenCaptureStandardOutputIsDisabled_DoesNotPreserveOutput ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            CreateOutputRequest("ignored", captureStandardOutput: false),
            CancellationToken.None);

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

        var result = await TestAwaiter.WaitAsync(
            runner.RunAsync(
                CreateLongRunningRequest(TimeSpan.Zero),
                cancellationTokenSource.Token),
            "Process runner caller cancellation race result",
            SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.Canceled, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenProcessExceedsTimeout_ReturnsTimedOut ()
    {
        var runner = new ProcessRunner();

        var result = await TestAwaiter.WaitAsync(
            runner.RunAsync(
                CreateLongRunningRequest(TimeSpan.FromMilliseconds(20)),
                CancellationToken.None),
            "Process runner timeout result",
            SignalWaitTimeout);

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

        var result = await TestAwaiter.WaitAsync(
            runner.RunAsync(
                new ProcessRunRequest(
                    FileName: invocation.FileName,
                    Arguments: invocation.Arguments,
                    Timeout: TimeSpan.FromMilliseconds(50),
                    OutputDrainMode: ProcessOutputDrainMode.BestEffort,
                    TerminationPolicy: new ProcessTerminationPolicy(
                        ProcessTerminationMode.GracefulThenKill,
                        TimeSpan.FromMilliseconds(250),
                        TimeSpan.FromMilliseconds(250))),
                CancellationToken.None),
            "Process runner graceful termination result",
            SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Equal(ProcessTerminationResult.GracefulExited, result.TerminationResult);
        Assert.True(File.Exists(markerPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WithGracefulThenKill_WhenProcessDoesNotExitGracefully_ReturnsForceKilled ()
    {
        var runner = new ProcessRunner();

        var result = await TestAwaiter.WaitAsync(
            runner.RunAsync(
                CreateGracefulThenKillNonResponsiveRequest(),
                CancellationToken.None),
            "Process runner force-kill fallback result",
            SignalWaitTimeout);

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

        var result = await TestAwaiter.WaitAsync(
            runner.RunAsync(
                CreateExitedProcessWithInheritedOutputHandleRequest(
                    timeout: TimeSpan.FromMilliseconds(250),
                    outputDrainMode: ProcessOutputDrainMode.BestEffort,
                    childLifetime: InheritedOutputHandleLifetime),
                CancellationToken.None),
            "Process runner inherited output handle result",
            TimeSpan.FromSeconds(8));

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

        var result = await TestAwaiter.WaitAsync(
            runner.RunAsync(
                CreateExitedProcessWithInheritedOutputHandleRequest(
                    timeout: TimeSpan.FromMilliseconds(20),
                    outputDrainMode: ProcessOutputDrainMode.WaitForCompletion,
                    childLifetime: TimeSpan.FromMilliseconds(100)),
                CancellationToken.None),
            "Process runner required output completion timeout result",
            SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    private static ProcessRunRequest CreateLongOutputRequest (bool captureStandardOutput)
    {
        return CreateOutputRequest(new string(LongOutputCharacter, LongOutputLength), captureStandardOutput);
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

    private static ProcessRunRequest CreateOutputRequest (
        string output,
        bool captureStandardOutput)
    {
        var invocation = TestProcessInvocations.CreateStandardOutput(output);

        return new ProcessRunRequest(
            FileName: invocation.FileName,
            Arguments: invocation.Arguments,
            Timeout: TimeSpan.FromSeconds(5),
            CaptureStandardOutput: captureStandardOutput);
    }
}
