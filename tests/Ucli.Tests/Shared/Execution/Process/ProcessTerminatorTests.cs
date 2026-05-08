namespace MackySoft.Ucli.Tests;

using System.Diagnostics;

public sealed class ProcessTerminatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TerminateAsync_WithForceKill_WaitsUntilProcessExits ()
    {
        using var process = StartLongRunningProcess();

        try
        {
            var result = await ProcessTerminator.TerminateAsync(
                process,
                new ProcessTerminationPolicy(
                    ProcessTerminationMode.ForceKill,
                    TimeSpan.FromMilliseconds(1),
                    TimeSpan.FromSeconds(5)),
                CancellationToken.None);

            Assert.Equal(ProcessTerminationResult.ForceKilled, result);
            Assert.True(process.HasExited);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static Process StartLongRunningProcess ()
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "powershell";
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("Start-Sleep -Seconds 30");
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("while :; do sleep 1; done");
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Test process could not be started.");
    }
}
