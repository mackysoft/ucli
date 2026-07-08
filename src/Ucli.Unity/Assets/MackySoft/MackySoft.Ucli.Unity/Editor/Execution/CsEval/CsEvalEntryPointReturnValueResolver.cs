using System;
using System.Reflection;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Resolves declared eval entry point return values before result serialization. </summary>
    internal static class CsEvalEntryPointReturnValueResolver
    {
        public static async Task<object?> ResolveAsync (
            Type declaredReturnType,
            object? invocationReturnValue)
        {
            // Preserve the Unity synchronization context for subsequent result serialization,
            // because JSON serialization may execute user-defined public getters.
            if (declaredReturnType == typeof(Task))
            {
                await GetRequiredTask(invocationReturnValue, declaredReturnType);
                return null;
            }

            if (IsGenericTask(declaredReturnType))
            {
                var task = GetRequiredTask(invocationReturnValue, declaredReturnType);
                await task;
                return GetTaskResult(task);
            }

            if (declaredReturnType == typeof(ValueTask))
            {
                if (invocationReturnValue == null)
                {
                    throw new CsEvalEntryPointReturnValueResolutionException("Entry point returned null for ValueTask.");
                }

                await ((ValueTask)invocationReturnValue);
                return null;
            }

            if (IsGenericValueTask(declaredReturnType))
            {
                var task = ConvertValueTaskToTask(declaredReturnType, invocationReturnValue);
                await task;
                return GetTaskResult(task);
            }

            return invocationReturnValue;
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
