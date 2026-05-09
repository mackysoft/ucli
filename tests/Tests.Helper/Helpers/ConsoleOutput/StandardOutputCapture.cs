namespace MackySoft.Tests;

/// <summary> Provides one process-wide standard-output capture utility for CLI-oriented tests. </summary>
internal static class StandardOutputCapture
{
    // NOTE: Console.Out is process-global, so tests that swap it must be serialized even when xUnit runs classes in parallel.
    private static readonly SemaphoreSlim CaptureLock = new(1, 1);

    /// <summary> Executes one asynchronous action while redirecting standard output to an in-memory writer. </summary>
    /// <param name="action"> The asynchronous action that writes to standard output. </param>
    /// <returns> The action exit code together with the captured standard-output text. </returns>
    internal static async Task<(int ExitCode, string StandardOutput)> ExecuteAsync (Func<Task<int>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await CaptureLock.WaitAsync();
        var originalOutput = Console.Out;

        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            var exitCode = await action();
            await writer.FlushAsync();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            CaptureLock.Release();
        }
    }
}
