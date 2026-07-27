namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>
/// Validates the runtime request DTO after strict System.Text.Json deserialization.
/// </summary>
internal static class UcliRequestJsonContractValidator
{
    public static bool TryValidate (
        IpcExecuteArgumentsJsonContract request,
        out int stepIndex,
        out string errorMessage)
    {
        return TryValidateSteps(request.Steps, out stepIndex, out errorMessage);
    }

    public static bool TryValidate (
        UcliRequestJsonContract request,
        out int stepIndex,
        out string errorMessage)
    {
        return TryValidateSteps(request.Steps, out stepIndex, out errorMessage);
    }

    private static bool TryValidateSteps (
        IReadOnlyList<UcliRequestStepJsonContract>? steps,
        out int stepIndex,
        out string errorMessage)
    {
        if (steps == null)
        {
            stepIndex = -1;
            errorMessage = "Request property '/steps' is required.";
            return false;
        }

        for (var i = 0; i < steps.Count; i++)
        {
            if (!TryValidateStep(steps[i], i, out errorMessage))
            {
                stepIndex = i;
                return false;
            }
        }

        stepIndex = -1;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateStep (
        UcliRequestStepJsonContract? step,
        int stepIndex,
        out string errorMessage)
    {
        var path = $"/steps/{stepIndex}";
        switch (step)
        {
            case UcliOperationRequestStepJsonContract operation:
                return TryValidateOperation(operation, path, out errorMessage);
            case UcliEditRequestStepJsonContract edit:
                return UcliEditRequestJsonContractValidator.TryValidate(edit, path, out errorMessage);
            case null:
                errorMessage = $"Request property '{path}' must be an object.";
                return false;
            default:
                errorMessage = $"Request property '{path}/kind' is unsupported.";
                return false;
        }
    }

    private static bool TryValidateOperation (
        UcliOperationRequestStepJsonContract operation,
        string path,
        out string errorMessage)
    {
        if (!UcliRequestJsonTextContractValidator.TryValidate(
                operation.Op,
                $"{path}/op",
                out errorMessage))
        {
            return false;
        }

        if (operation.Args == null)
        {
            errorMessage = $"Request property '{path}/args' is required.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
