using MackySoft.Ucli.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task RunAsync_WithInvalidExecutable_ReturnsStartFailed ()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            new ProcessRunRequest(
                FileName: "__ucli_missing_executable__",
                Arguments: Array.Empty<string>(),
                TimeoutSeconds: 1),
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
                TimeoutSeconds: 5,
                CaptureStandardOutput: captureStandardOutput);
        }

        return new ProcessRunRequest(
            FileName: "/bin/sh",
            Arguments:
            [
                "-c",
                "i=0; while [ \"$i\" -lt 5000 ]; do printf x; i=$((i+1)); done; printf '\\n'",
            ],
            TimeoutSeconds: 5,
            CaptureStandardOutput: captureStandardOutput);
    }
}