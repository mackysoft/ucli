using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads candidate-source edit selections declared with <c>step.select.from</c>. </summary>
internal static class IpcEditStepFromSelectionReader
{
    private static readonly ISet<string> AllowedFromSelectionProperties = new HashSet<string>
    {
        "cardinality",
        "from",
    };

    private static readonly ISet<string> AllowedFromProperties = new HashSet<string>
    {
        "op",
        "args",
    };

    public static bool TryRead (
        JsonElement selectElement,
        JsonElement fromElement,
        IpcEditStepContract.ContextKind contextKind,
        IpcEditStepContract.CardinalityKind cardinality,
        out IpcEditStepContract.EditSelection selection,
        out string errorMessage)
    {
        selection = default!;
        if (!TryValidateSelectWrapper(selectElement, contextKind, out errorMessage)
            || !TryValidateFromObject(fromElement, out errorMessage)
            || !TryReadSourceOperation(fromElement, cardinality, out var sourceOperation, out errorMessage)
            || !TryReadSourceArgs(fromElement, out var sourceArgs, out errorMessage))
        {
            return false;
        }

        selection = CreateSelection(cardinality, sourceOperation!, sourceArgs);
        return true;
    }

    private static bool TryValidateSelectWrapper (
        JsonElement selectElement,
        IpcEditStepContract.ContextKind contextKind,
        out string errorMessage)
    {
        if (contextKind != IpcEditStepContract.ContextKind.Scene)
        {
            errorMessage = "Edit step property 'step.select.from' is supported only for scene context.";
            return false;
        }

        return TryValidateSelectProperties(selectElement, out errorMessage);
    }

    private static bool TryValidateSelectProperties (
        JsonElement selectElement,
        out string errorMessage)
    {
        if (HasDirectSelectorProperty(selectElement))
        {
            errorMessage = "Edit step property 'step.select' cannot mix 'from' with direct selector fields.";
            return false;
        }

        var unknownSelectProperty = JsonObjectPropertyReader.FindUnknownProperty(selectElement, AllowedFromSelectionProperties);
        errorMessage = unknownSelectProperty is null
            ? string.Empty
            : $"Edit step property 'step.select' contains an unknown property: {unknownSelectProperty}.";
        return unknownSelectProperty is null;
    }

    private static bool TryValidateFromObject (
        JsonElement fromElement,
        out string errorMessage)
    {
        if (fromElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "Edit step property 'step.select.from' must be an object.";
            return false;
        }

        return TryValidateFromProperties(fromElement, out errorMessage);
    }

    private static bool TryValidateFromProperties (
        JsonElement fromElement,
        out string errorMessage)
    {
        var unknownFromProperty = JsonObjectPropertyReader.FindUnknownProperty(fromElement, AllowedFromProperties);
        errorMessage = unknownFromProperty is null
            ? string.Empty
            : $"Edit step property 'step.select.from' contains an unknown property: {unknownFromProperty}.";
        return unknownFromProperty is null;
    }

    private static bool TryReadSourceOperation (
        JsonElement fromElement,
        IpcEditStepContract.CardinalityKind cardinality,
        out string? sourceOperation,
        out string errorMessage)
    {
        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(fromElement, "op", "step.select.from.op", out sourceOperation, out errorMessage))
        {
            return false;
        }

        return TryValidateSourceOperation(sourceOperation!, cardinality, out errorMessage);
    }

    private static bool TryValidateSourceOperation (
        string sourceOperation,
        IpcEditStepContract.CardinalityKind cardinality,
        out string errorMessage)
    {
        if (!string.Equals(sourceOperation, UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal))
        {
            errorMessage = $"Edit step property 'step.select.from.op' must be '{UcliPrimitiveOperationNames.SceneQuery}'.";
            return false;
        }

        if (cardinality == IpcEditStepContract.CardinalityKind.First)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryReadSourceArgs (
        JsonElement fromElement,
        out JsonElement sourceArgs,
        out string errorMessage)
    {
        if (!IpcEditStepContractReadHelpers.TryReadRequiredObject(fromElement, "args", "step.select.from.args", out sourceArgs, out errorMessage))
        {
            return false;
        }

        return IpcSceneQueryArgsContractReader.TryReadForEditSelection(sourceArgs, out _, out errorMessage);
    }

    private static IpcEditStepContract.EditSelection CreateSelection (
        IpcEditStepContract.CardinalityKind cardinality,
        string sourceOperation,
        JsonElement sourceArgs)
    {
        return new IpcEditStepContract.EditSelection(
            IpcEditStepContract.SelectionKind.From,
            cardinality,
            GameObjectPath: null,
            ComponentType: null,
            Self: false,
            ProjectAssetPath: null,
            SourceOperation: sourceOperation,
            SourceArgs: sourceArgs.Clone());
    }

    private static bool HasDirectSelectorProperty (JsonElement selectElement)
    {
        return selectElement.TryGetProperty("gameObject", out _)
               || selectElement.TryGetProperty("component", out _)
               || selectElement.TryGetProperty("self", out _)
               || selectElement.TryGetProperty("projectAsset", out _);
    }
}
