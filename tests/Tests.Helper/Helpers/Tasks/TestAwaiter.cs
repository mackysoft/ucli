using Xunit.Sdk;

namespace MackySoft.Tests;

/// <summary>
/// Bounded wait helper for test-owned asynchronous work.
/// Use this for signals and result tasks that should complete inside the test flow.
/// </summary>
internal static class TestAwaiter
{
    internal static async Task WaitAsync (
        Task task,
        string description,
        TimeSpan timeout)
    {
        await AwaitCompletionAsync(task, description, timeout).ConfigureAwait(false);
        await task.ConfigureAwait(false);
    }

    internal static async Task<T> WaitAsync<T> (
        Task<T> task,
        string description,
        TimeSpan timeout)
    {
        await AwaitCompletionAsync(task, description, timeout).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }

    private static async Task AwaitCompletionAsync (
        Task task,
        string description,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        // NOTE: This timeout is a test-only fuse so stalled async test flows fail fast instead of stalling the runner.
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (!ReferenceEquals(completedTask, task))
        {
            throw new XunitException($"{description} did not complete within {timeout}.");
        }
    }
}
