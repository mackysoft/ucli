using System;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Parses one public <c>kind:"edit"</c> step into a structural contract model. </summary>
internal static class IpcEditStepContractReader
{
    /// <summary>
    /// Parses one public <c>kind:"edit"</c> step into a structural contract model.
    /// </summary>
    /// <param name="stepElement"> The cloned public step JSON object. </param>
    /// <param name="stepContract"> The parsed edit-step contract when parsing succeeds. </param>
    /// <param name="errorMessage"> The validation error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when the step matches the public edit-step contract; otherwise <see langword="false" />. </returns>
    public static bool TryRead (
        JsonElement stepElement,
        out IpcEditStepContract stepContract,
        out string errorMessage)
    {
        stepContract = default!;
        errorMessage = string.Empty;
        if (stepElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = "Edit step must be an object.";
            return false;
        }

        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            stepElement,
            "id",
            "step.id",
            out var stepId,
            out errorMessage))
        {
            return false;
        }

        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(
            stepElement,
            "kind",
            "step.kind",
            out var kindLiteral,
            out errorMessage))
        {
            return false;
        }

        if (!string.Equals(kindLiteral, "edit", StringComparison.Ordinal))
        {
            errorMessage = "Edit step property 'kind' must be 'edit'.";
            return false;
        }

        if (!IpcEditStepContextReader.TryRead(stepElement, out var context, out errorMessage))
        {
            return false;
        }

        if (!IpcEditStepSelectionReader.TryRead(stepElement, context.Kind, out var selection, out errorMessage))
        {
            return false;
        }

        if (!IpcEditStepActionsReader.TryRead(stepElement, out var actions, out errorMessage))
        {
            return false;
        }

        if (!IpcEditStepCommitReader.TryRead(stepElement, out var commit, out errorMessage))
        {
            return false;
        }

        stepContract = new IpcEditStepContract(stepId!, context, selection, actions, commit);
        return true;
    }
}
