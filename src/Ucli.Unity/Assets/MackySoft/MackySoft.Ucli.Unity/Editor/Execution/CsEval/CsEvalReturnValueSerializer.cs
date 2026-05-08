using System;
using System.Reflection;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Converts eval entry point return values to the operation result contract. </summary>
    internal sealed class CsEvalReturnValueSerializer
    {
        public bool TrySerialize (
            MethodInfo method,
            object? value,
            out CsEvalReturnValue returnValue,
            out string errorMessage)
        {
            if (method.ReturnType == typeof(void))
            {
                returnValue = new CsEvalReturnValue(CsEvalReturnValueKindValues.Void, value: null);
                errorMessage = string.Empty;
                return true;
            }

            if (value == null)
            {
                returnValue = new CsEvalReturnValue(CsEvalReturnValueKindValues.Null, value: null);
                errorMessage = string.Empty;
                return true;
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

                returnValue = new CsEvalReturnValue(CsEvalReturnValueKindValues.Json, jsonValue);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
            {
                returnValue = null!;
                errorMessage = $"Entry point return value is not JSON-serializable. {exception.Message}";
                return false;
            }
        }
    }
}
