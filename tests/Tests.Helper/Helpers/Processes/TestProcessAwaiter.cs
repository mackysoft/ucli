using System.Diagnostics;
using Xunit.Sdk;

namespace MackySoft.Tests;

/// <summary>
/// Bounded wait helper for child process termination in tests.
/// Use this instead of waiting on <see cref="Process"/> directly.
/// </summary>
internal static class TestProcessAwaiter
{
    internal static async Task WaitForExitAsync (
        Process process,
        string description,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var exitTask = process.WaitForExitAsync();

        // NOTE: This timeout is a test-only fuse so stalled child processes fail fast instead of stalling the runner.
        var completedTask = await Task.WhenAny(exitTask, Task.Delay(timeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completedTask, exitTask))
        {
            TryTerminate(process);
            throw new XunitException($"{description} did not exit within {timeout}.");
        }

        await exitTask.ConfigureAwait(false);
    }

    private static void TryTerminate (Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
