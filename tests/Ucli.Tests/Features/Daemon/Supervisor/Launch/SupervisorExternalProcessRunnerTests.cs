using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorExternalProcessRunnerTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunIgnoringExitCode_WhenProcessExitsNonZero_DoesNotThrow ()
    {
        var runner = new SupervisorExternalProcessRunner();
        var invocation = TestProcessInvocations.CreateNonZeroExit();

        await runner.RunIgnoringExitCodeAsync(
            invocation.FileName,
            invocation.Arguments,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunIgnoringExitCode_WhenProcessCannotStart_DoesNotThrow ()
    {
        var runner = new SupervisorExternalProcessRunner();

        await runner.RunIgnoringExitCodeAsync(
            "definitely-missing-ucli-command",
            Array.Empty<string>(),
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunIgnoringExitCode_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        var runner = new SupervisorExternalProcessRunner();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                runner.RunIgnoringExitCodeAsync(
                    "dotnet",
                    ["--info"],
                    cancellationTokenSource.Token).AsTask(),
                "Canceled supervisor external process run",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Run_WhenCanceledAfterProcessStarts_TerminatesProcessBeforeRethrowing ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-external-process-runner",
            "cancel-started-process");
        var readyPath = scope.GetPath("ready");
        var processIdPath = scope.GetPath("pid");
        var invocation = CreateReadyLongRunningInvocation(readyPath, processIdPath);
        using var cancellationTokenSource = new CancellationTokenSource();
        var runner = new SupervisorExternalProcessRunner();
        var runTask = runner.RunAsync(
                invocation.FileName,
                invocation.Arguments,
                cancellationTokenSource.Token)
            .AsTask();
        await WaitForFileExistsAsync(readyPath, AsyncWaitTimeout);
        var processId = int.Parse(
            await File.ReadAllTextAsync(processIdPath, CancellationToken.None),
            System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => TestAwaiter.WaitAsync(
                    runTask,
                    "Canceled supervisor helper process",
                    AsyncWaitTimeout));

            await WaitForProcessExitAsync(processId, AsyncWaitTimeout);
        }
        finally
        {
            TryKillProcess(processId);
        }
    }

    private static TestProcessInvocation CreateReadyLongRunningInvocation (
        string readyPath,
        string processIdPath)
    {
        if (OperatingSystem.IsWindows())
        {
            var escapedReadyPath = readyPath.Replace("'", "''", StringComparison.Ordinal);
            var escapedProcessIdPath = processIdPath.Replace("'", "''", StringComparison.Ordinal);
            return new TestProcessInvocation(
                "powershell",
                [
                    "-NoProfile",
                    "-Command",
                    $"Set-Content -LiteralPath '{escapedProcessIdPath}' -Value $PID -NoNewline; "
                    + $"Set-Content -LiteralPath '{escapedReadyPath}' -Value ready -NoNewline; "
                    + "Start-Sleep -Seconds 30",
                ]);
        }

        return new TestProcessInvocation(
            "/bin/sh",
            [
                "-c",
                $"printf '%s' $$ > {TestShellPaths.QuoteBashArgument(processIdPath)}; "
                + $"printf ready > {TestShellPaths.QuoteBashArgument(readyPath)}; "
                + "sleep 30",
            ]);
    }

    private static async Task WaitForFileExistsAsync (
        string path,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }

        Assert.Fail($"File was not created within {timeout}: {path}");
    }

    private static async Task WaitForProcessExitAsync (
        int processId,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (HasExited(processId))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }

        Assert.Fail($"Process did not exit within {timeout}. processId={processId}.");
    }

    private static bool HasExited (int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static void TryKillProcess (int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (ArgumentException)
        {
        }
    }
}
