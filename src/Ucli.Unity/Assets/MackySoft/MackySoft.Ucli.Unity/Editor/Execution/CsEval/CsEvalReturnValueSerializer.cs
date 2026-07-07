using System;
using System.Text.Json;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Converts eval entry point return values to the operation result contract. </summary>
    internal sealed class CsEvalReturnValueSerializer
    {
        public bool TrySerialize (
            object? value,
            out CsEvalReturnValue returnValue,
            out string errorMessage)
        {
            if (value == null)
            {
                returnValue = new CsEvalReturnValue(CsEvalReturnValueKindValues.Null, value: null);
                errorMessage = string.Empty;
                return true;
            }

            if (IsTaskLike(value.GetType()))
            {
                returnValue = null!;
                errorMessage = "Entry point returned an unawaited Task or ValueTask value. Declare the Run return type as Task, Task<T>, ValueTask, or ValueTask<T> to have eval await it.";
                return false;
            }

            try
            {
                JsonElement jsonValue = value is JsonElement jsonElement
                    ? jsonElement.Clone()
                    : JsonSerializer.SerializeToElement(value, value.GetType(), IpcJsonSerializerOptions.Default);
                if (jsonValue.ValueKind == JsonValueKind.Undefined)
                {
                    returnValue = null!;
                    errorMessage = "Entry point returned an undefined JSON value.";
                    return false;
                }

                var valueBytes = JsonSerializer.SerializeToUtf8Bytes(jsonValue, IpcJsonSerializerOptions.Default);
                if (valueBytes.Length > CsEvalSafetyLimits.MaxReturnValueBytes)
                {
                    returnValue = null!;
                    errorMessage = "Entry point return value exceeds internal IPC safety guardrail.";
                    return false;
                }

                returnValue = new CsEvalReturnValue(CsEvalReturnValueKindValues.Json, jsonValue);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                returnValue = null!;
                errorMessage = $"Entry point return value is not JSON-serializable. {exception.GetType().FullName}: {exception.Message}";
                return false;
            }
        }

        private static bool IsTaskLike (Type valueType)
        {
            return typeof(Task).IsAssignableFrom(valueType)
                || valueType == typeof(ValueTask)
                || (valueType.IsGenericType
                    && valueType.GetGenericTypeDefinition() == typeof(ValueTask<>));
        }
    }
}
