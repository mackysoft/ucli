using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary>
    /// Bounded wait helper for test-owned asynchronous work inside Unity tests.
    /// Use this for signals and result tasks that should complete inside the test flow.
    /// </summary>
    internal static class TestAwaiter
    {
        public static async Task WaitAsync (
            Task task,
            string description,
            TimeSpan timeout)
        {
            await AwaitCompletionAsync(task, description, timeout);
            await task;
        }

        public static async Task<T> WaitAsync<T> (
            Task<T> task,
            string description,
            TimeSpan timeout)
        {
            await AwaitCompletionAsync(task, description, timeout);
            return await task;
        }

        public static async UniTask WaitAsync (
            UniTask task,
            string description,
            TimeSpan timeout)
        {
            var taskInstance = task.AsTask();
            await AwaitCompletionAsync(taskInstance, description, timeout);
            await taskInstance;
        }

        public static async UniTask<T> WaitAsync<T> (
            UniTask<T> task,
            string description,
            TimeSpan timeout)
        {
            var taskInstance = task.AsTask();
            await AwaitCompletionAsync(taskInstance, description, timeout);
            return await taskInstance;
        }

        private static async Task AwaitCompletionAsync (
            Task task,
            string description,
            TimeSpan timeout)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description must not be null or whitespace.", nameof(description));
            }

            // NOTE: This timeout is a test-only fuse so stalled async test flows fail fast instead of stalling the Unity job.
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
            Assert.That(
                completedTask,
                Is.SameAs(task),
                $"{description} did not complete within {timeout}.");
        }
    }
}