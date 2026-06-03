using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads the <c>actions</c> array for one public edit step. </summary>
internal static class IpcEditStepActionsReader
{
    /// <summary> Reads and validates the <c>actions</c> array for one public edit step. </summary>
    /// <param name="stepElement"> The raw step JSON element. </param>
    /// <param name="actions"> The parsed action contracts when validation succeeds. </param>
    /// <param name="errorMessage"> The contract violation message when validation fails. </param>
    /// <returns> <see langword="true" /> when the step contains one valid non-empty action array; otherwise <see langword="false" />. </returns>
    public static bool TryRead (
        JsonElement stepElement,
        out IReadOnlyList<IpcEditStepContract.EditAction> actions,
        out string errorMessage)
    {
        actions = Array.Empty<IpcEditStepContract.EditAction>();
        if (!IpcEditStepContractReadHelpers.TryReadRequiredArray(
            stepElement,
            "actions",
            "step.actions",
            out var actionsElement,
            out errorMessage))
        {
            return false;
        }

        return TryReadArray(actionsElement, out actions, out errorMessage);
    }

    private static bool TryReadArray (
        JsonElement actionsElement,
        out IReadOnlyList<IpcEditStepContract.EditAction> actions,
        out string errorMessage)
    {
        actions = Array.Empty<IpcEditStepContract.EditAction>();
        var parsedActions = new List<IpcEditStepContract.EditAction>();
        var actionIndex = 0;
        foreach (var actionElement in actionsElement.EnumerateArray())
        {
            if (actionElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Edit step property 'step.actions[{actionIndex}]' must be an object.";
                return false;
            }

            if (!TryReadAction(actionElement, actionIndex, out var action, out errorMessage))
            {
                return false;
            }

            parsedActions.Add(action);
            actionIndex++;
        }

        if (parsedActions.Count == 0)
        {
            errorMessage = "Edit step property 'step.actions' must contain at least one action.";
            return false;
        }

        actions = parsedActions;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryReadAction (
        JsonElement actionElement,
        int actionIndex,
        out IpcEditStepContract.EditAction action,
        out string errorMessage)
    {
        action = default!;
        if (!TryReadActionKind(actionElement, actionIndex, out var actionKind, out errorMessage))
        {
            return false;
        }

        if (!IpcEditStepActionPropertyReader.TryRead(actionElement, actionIndex, actionKind, out var state, out errorMessage)
            || !IpcEditStepActionShapeValidator.TryValidate(actionElement, actionIndex, state, out errorMessage))
        {
            return false;
        }

        action = state.ToAction();
        return true;
    }

    private static bool TryReadActionKind (
        JsonElement actionElement,
        int actionIndex,
        out IpcEditStepContract.ActionKind actionKind,
        out string errorMessage)
    {
        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            actionElement,
            "kind",
            $"step.actions[{actionIndex}].kind",
            out var kindLiteral,
            out errorMessage))
        {
            actionKind = default;
            return false;
        }

        if (!ContractLiteralCodec.TryParse(kindLiteral, out actionKind))
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].kind' is unsupported.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
