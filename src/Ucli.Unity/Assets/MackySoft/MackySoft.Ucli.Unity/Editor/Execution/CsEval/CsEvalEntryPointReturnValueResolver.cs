using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Resolves declared eval entry point return values before result serialization. </summary>
    internal static class CsEvalEntryPointReturnValueResolver
    {
        public static async Task<object?> ResolveAsync (
            Type declaredReturnType,
            object? invocationReturnValue,
            CancellationToken cancellationToken,
            Action<string, Task> quarantineMutationLane)
        {
            if (quarantineMutationLane == null)
            {
                throw new ArgumentNullException(nameof(quarantineMutationLane));
            }

            // Preserve the Unity synchronization context for subsequent result serialization,
            // because JSON serialization may execute user-defined public getters.
            if (declaredReturnType == typeof(Task))
            {
                await AwaitTaskAsync(GetRequiredTask(invocationReturnValue, declaredReturnType), cancellationToken, quarantineMutationLane);
                return null;
            }

            if (IsGenericTask(declaredReturnType))
            {
                var task = GetRequiredTask(invocationReturnValue, declaredReturnType);
                await AwaitTaskAsync(task, cancellationToken, quarantineMutationLane);
                return GetTaskResult(task);
            }

            if (declaredReturnType == typeof(ValueTask))
            {
                if (invocationReturnValue == null)
                {
                    throw new CsEvalEntryPointReturnValueResolutionException("Entry point returned null for ValueTask.");
                }

                await AwaitTaskAsync(((ValueTask)invocationReturnValue).AsTask(), cancellationToken, quarantineMutationLane);
                return null;
            }

            if (IsGenericValueTask(declaredReturnType))
            {
                var task = ConvertValueTaskToTask(declaredReturnType, invocationReturnValue);
                await AwaitTaskAsync(task, cancellationToken, quarantineMutationLane);
                return GetTaskResult(task);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return invocationReturnValue;
        }

        private static async Task AwaitTaskAsync (
            Task task,
            CancellationToken cancellationToken,
            Action<string, Task> quarantineMutationLane)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await AwaitCancellationQuiescenceAsync(task, quarantineMutationLane).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!cancellationToken.CanBeCanceled)
            {
                await task;
                return;
            }

            var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(static state =>
            {
                var source = (TaskCompletionSource<bool>)state!;
                source.TrySetResult(true);
            }, cancellationSource))
            {
                var completedTask = await Task.WhenAny(task, cancellationSource.Task);
                if (!ReferenceEquals(completedTask, task))
                {
                    await AwaitCancellationQuiescenceAsync(task, quarantineMutationLane).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            await task;
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async Task AwaitCancellationQuiescenceAsync (
            Task task,
            Action<string, Task> quarantineMutationLane)
        {
            ObserveFault(task);
            var didQuiesce = await UnityMutationCancellationPolicy
                .WaitForQuiescenceAsync(task)
                .ConfigureAwait(false);
            if (!didQuiesce)
            {
                quarantineMutationLane(
                    "A C# eval task remained active after request cancellation and may still mutate Unity state.",
                    task);
            }
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private static bool IsGenericTask (Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);
        }

        private static bool IsGenericValueTask (Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>);
        }

        private static Task GetRequiredTask (
            object? value,
            Type declaredReturnType)
        {
            if (value is Task task)
            {
                return task;
            }

            throw new CsEvalEntryPointReturnValueResolutionException($"Entry point returned null for {declaredReturnType.Name}.");
        }

        private static Task ConvertValueTaskToTask (
            Type declaredReturnType,
            object? value)
        {
            if (value == null)
            {
                throw new CsEvalEntryPointReturnValueResolutionException($"Entry point returned null for {declaredReturnType.Name}.");
            }

            var asTaskMethod = declaredReturnType.GetMethod(nameof(ValueTask<int>.AsTask), Type.EmptyTypes);
            if (asTaskMethod == null)
            {
                throw new CsEvalEntryPointReturnValueResolutionException($"Entry point return type '{declaredReturnType.FullName}' does not expose AsTask().");
            }

            if (asTaskMethod.Invoke(value, Array.Empty<object>()) is Task task)
            {
                return task;
            }

            throw new CsEvalEntryPointReturnValueResolutionException($"Entry point return type '{declaredReturnType.FullName}' did not produce a Task.");
        }

        private static object? GetTaskResult (Task task)
        {
            var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (resultProperty == null)
            {
                throw new CsEvalEntryPointReturnValueResolutionException($"Entry point return task type '{task.GetType().FullName}' does not expose Result.");
            }

            return resultProperty.GetValue(task);
        }
    }
}
