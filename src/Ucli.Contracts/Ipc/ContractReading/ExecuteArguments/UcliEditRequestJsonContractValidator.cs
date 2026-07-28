namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Validates the edit DTO aggregate and delegates its owned subcontracts.</summary>
internal static class UcliEditRequestJsonContractValidator
{
    public static bool TryValidate (
        UcliEditRequestStepJsonContract edit,
        string path,
        out string errorMessage)
    {
        if (edit.On == null)
        {
            errorMessage = $"Request property '{path}/on' is required.";
            return false;
        }

        if (!UcliEditSelectionJsonContractValidator.TryValidateContext(
                edit.On,
                $"{path}/on",
                out var contextKind,
                out errorMessage))
        {
            return false;
        }

        if (edit.Select == null)
        {
            errorMessage = $"Request property '{path}/select' is required.";
            return false;
        }

        if (!UcliEditSelectionJsonContractValidator.TryValidateSelection(
                edit.Select,
                contextKind,
                $"{path}/select",
                out errorMessage))
        {
            return false;
        }

        return TryValidateActions(edit.Actions, path, out errorMessage);
    }

    private static bool TryValidateActions (
        IReadOnlyList<UcliEditActionJsonContract>? actions,
        string path,
        out string errorMessage)
    {
        if (actions == null || actions.Count == 0)
        {
            errorMessage = $"Request property '{path}/actions' must contain at least one action.";
            return false;
        }

        for (var i = 0; i < actions.Count; i++)
        {
            if (!UcliEditActionJsonContractValidator.TryValidate(
                    actions[i],
                    $"{path}/actions/{i}",
                    out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}
