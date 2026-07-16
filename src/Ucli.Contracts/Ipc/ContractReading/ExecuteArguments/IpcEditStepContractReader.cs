using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

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

        if (!TryReadHeader(stepElement, out var stepId, out errorMessage))
        {
            return false;
        }

        if (!TryReadBody(stepElement, out var body, out errorMessage))
        {
            return false;
        }

        stepContract = new IpcEditStepContract(stepId!, body.Context, body.Selection, body.Actions, body.Commit);
        return true;
    }

    private static bool TryReadHeader (
        JsonElement stepElement,
        out IpcExecuteStepId? stepId,
        out string errorMessage)
    {
        stepId = null;
        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(stepElement, "id", "step.id", out var stepIdValue, out errorMessage))
        {
            return false;
        }

        stepId = new IpcExecuteStepId(stepIdValue!);

        if (!IpcEditStepContractReadHelpers.TryReadRequiredString(stepElement, "kind", "step.kind", out var kindLiteral, out errorMessage))
        {
            return false;
        }

        if (!string.Equals(kindLiteral, "edit", StringComparison.Ordinal))
        {
            errorMessage = "Edit step property 'kind' must be 'edit'.";
            return false;
        }

        return true;
    }

    private static bool TryReadBody (
        JsonElement stepElement,
        out Body body,
        out string errorMessage)
    {
        body = default;
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

        body = new Body(context, selection, actions, commit);
        return true;
    }

    private readonly struct Body
    {
        public Body (
            IpcEditStepContract.EditContext context,
            IpcEditStepContract.EditSelection selection,
            IReadOnlyList<IpcEditStepContract.EditAction> actions,
            IpcEditStepContract.CommitKind commit)
        {
            Context = context;
            Selection = selection;
            Actions = actions;
            Commit = commit;
        }

        public IpcEditStepContract.EditContext Context { get; }

        public IpcEditStepContract.EditSelection Selection { get; }

        public IReadOnlyList<IpcEditStepContract.EditAction> Actions { get; }

        public IpcEditStepContract.CommitKind Commit { get; }
    }
}
