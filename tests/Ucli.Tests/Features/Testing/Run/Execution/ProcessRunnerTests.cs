using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

public sealed class ProcessRunnerTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
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
    [Trait("Size", "Small")]
    public async Task RunAsync_WhenCaptureStandardOutputIsEnabled_PreservesFullOutput ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            CreateLongOutputRequest(captureStandardOutput: true),
            CancellationToken.None);

        Assert.Equal(ProcessRunStatus.Exited, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.StandardOutput);
        Assert.Equal(5000, result.StandardOutput!.TrimEnd('\r', '\n').Length);
        Assert.Equal(new string('x', 5000), result.StandardOutput.TrimEnd('\r', '\n'));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RunAsync_WhenCaptureStandardOutputIsDisabled_DoesNotPreserveOutput ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            CreateLongOutputRequest(captureStandardOutput: false),
            CancellationToken.None);

        Assert.Equal(ProcessRunStatus.Exited, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.StandardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
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
    [Trait("Size", "Small")]
    public async Task RunAsync_WhenProcessExceedsTimeout_ReturnsTimedOut ()
    {
        var runner = new ProcessRunner();

        var result = await TestAwaiter.WaitAsync(
            runner.RunAsync(
                CreateLongRunningRequest(TimeSpan.FromMilliseconds(200)),
                CancellationToken.None),
            "Process runner timeout result",
            SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    [Trait("Size", "Small")]
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
                    TimeSpan.FromSeconds(10),
                    ProcessOutputDrainMode.BestEffort),
                CancellationToken.None),
            "Process runner inherited output handle result",
            TimeSpan.FromSeconds(8));

        Assert.Equal(ProcessRunStatus.Exited, result.Status);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    [Trait("Size", "Small")]
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
                    TimeSpan.FromMilliseconds(200),
                    ProcessOutputDrainMode.WaitForCompletion),
                CancellationToken.None),
            "Process runner required output completion timeout result",
            SignalWaitTimeout);

        Assert.Equal(ProcessRunStatus.TimedOut, result.Status);
        Assert.Null(result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    private static ProcessRunRequest CreateLongOutputRequest (bool captureStandardOutput)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessRunRequest(
                FileName: "powershell",
                Arguments:
                [
                    "-NoProfile",
                    "-Command",
                    "Write-Output ('x' * 5000)",
                ],
                Timeout: TimeSpan.FromSeconds(5),
                CaptureStandardOutput: captureStandardOutput);
        }

        return new ProcessRunRequest(
            FileName: "/bin/sh",
            Arguments:
            [
                "-c",
                "i=0; while [ \"$i\" -lt 5000 ]; do printf x; i=$((i+1)); done; printf '\\n'",
            ],
            Timeout: TimeSpan.FromSeconds(5),
            CaptureStandardOutput: captureStandardOutput);
    }

    private static ProcessRunRequest CreateLongRunningRequest (
        TimeSpan timeout,
        ProcessOutputDrainMode outputDrainMode = ProcessOutputDrainMode.WaitForCompletion)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessRunRequest(
                FileName: "powershell",
                Arguments:
                [
                    "-NoProfile",
                    "-Command",
                    "Start-Sleep -Seconds 30",
                ],
                Timeout: timeout,
                OutputDrainMode: outputDrainMode);
        }

        return new ProcessRunRequest(
            FileName: "/bin/sh",
            Arguments:
            [
                "-c",
                "sleep 30",
            ],
            Timeout: timeout,
            OutputDrainMode: outputDrainMode);
    }

    private static ProcessRunRequest CreateExitedProcessWithInheritedOutputHandleRequest (
        TimeSpan timeout,
        ProcessOutputDrainMode outputDrainMode)
    {
        return new ProcessRunRequest(
            FileName: "/bin/sh",
            Arguments:
            [
                "-c",
                "sleep 15 & exit 0",
            ],
            Timeout: timeout,
            OutputDrainMode: outputDrainMode);
    }
}
