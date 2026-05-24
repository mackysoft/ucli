using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads public edit action properties after the action kind has been resolved. </summary>
internal static class IpcEditStepActionPropertyReader
{
    private static readonly string[] OptionalStringProperties =
    {
        "target",
        "as",
        "type",
        "name",
        "path",
        "parent",
        "targetAssetPath",
    };

    public static bool TryRead (
        JsonElement actionElement,
        int actionIndex,
        IpcEditStepContract.ActionKind actionKind,
        out IpcEditStepActionReadState state,
        out string errorMessage)
    {
        state = default;
        if (!TryReadStrings(actionElement, actionIndex, out var strings, out errorMessage)
            || !IpcEditStepPropertyPathsReader.TryReadOptional(actionElement, actionIndex, out var propertyPaths, out errorMessage)
            || !TryReadValues(actionElement, actionIndex, out var hasValues, out var values, out errorMessage))
        {
            return false;
        }

        state = CreateState(actionKind, strings, propertyPaths, hasValues, values);
        return true;
    }

    private static bool TryReadStrings (
        JsonElement actionElement,
        int actionIndex,
        out string?[] values,
        out string errorMessage)
    {
        values = new string?[OptionalStringProperties.Length];
        for (var i = 0; i < OptionalStringProperties.Length; i++)
        {
            values[i] = IpcEditStepContractReadHelpers.TryReadOptionalString(
                actionElement,
                OptionalStringProperties[i],
                $"step.actions[{actionIndex}].{OptionalStringProperties[i]}",
                out errorMessage);
            if (errorMessage.Length != 0)
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryReadValues (
        JsonElement actionElement,
        int actionIndex,
        out bool hasValues,
        out JsonElement values,
        out string errorMessage)
    {
        values = default;
        hasValues = false;
        if (!actionElement.TryGetProperty("values", out var valuesElement))
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryCloneValues(valuesElement, actionIndex, out hasValues, out values, out errorMessage);
    }

    private static bool TryCloneValues (
        JsonElement valuesElement,
        int actionIndex,
        out bool hasValues,
        out JsonElement values,
        out string errorMessage)
    {
        values = default;
        hasValues = false;
        if (valuesElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].values' must be an object.";
            return false;
        }

        hasValues = true;
        values = valuesElement.Clone();
        errorMessage = string.Empty;
        return true;
    }

    private static IpcEditStepActionReadState CreateState (
        IpcEditStepContract.ActionKind actionKind,
        IReadOnlyList<string?> values,
        IReadOnlyList<string>? propertyPaths,
        bool hasValues,
        JsonElement valueElement)
    {
        return new IpcEditStepActionReadState(
            actionKind,
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5],
            values[6],
            propertyPaths,
            hasValues,
            valueElement);
    }
}
