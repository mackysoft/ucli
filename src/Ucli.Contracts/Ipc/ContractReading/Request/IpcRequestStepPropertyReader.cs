using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads low-level public request step properties with normalized error kinds. </summary>
internal static class IpcRequestStepPropertyReader
{
    public static bool TryReadRequiredObject (
        JsonElement jsonObject,
        string propertyName,
        bool requireProperty,
        out StepPropertyReadErrorKind errorKind)
    {
        return TryReadRequiredProperty(jsonObject, propertyName, requireProperty, JsonValueKind.Object, out _, out errorKind);
    }

    public static bool TryReadRequiredArray (
        JsonElement jsonObject,
        string propertyName,
        bool requireProperty,
        out JsonElement propertyElement,
        out StepPropertyReadErrorKind errorKind)
    {
        return TryReadRequiredProperty(jsonObject, propertyName, requireProperty, JsonValueKind.Array, out propertyElement, out errorKind);
    }

    public static bool ValidateActionElements (
        JsonElement actionsElement,
        int stepIndex,
        string? stepId,
        out IpcRequestContractReadError error)
    {
        foreach (var actionElement in actionsElement.EnumerateArray())
        {
            if (actionElement.ValueKind != JsonValueKind.Object)
            {
                error = IpcRequestContractReadError.StepActionMustBeObject(stepIndex, stepId);
                return false;
            }
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadRequiredProperty (
        JsonElement jsonObject,
        string propertyName,
        bool requireProperty,
        JsonValueKind requiredKind,
        out JsonElement propertyElement,
        out StepPropertyReadErrorKind errorKind)
    {
        propertyElement = default;
        if (!jsonObject.TryGetProperty(propertyName, out propertyElement))
        {
            errorKind = requireProperty
                ? StepPropertyReadErrorKind.Missing
                : StepPropertyReadErrorKind.None;
            return false;
        }

        if (propertyElement.ValueKind != requiredKind)
        {
            errorKind = StepPropertyReadErrorKind.TypeMismatch;
            return false;
        }

        errorKind = StepPropertyReadErrorKind.None;
        return true;
    }
}
