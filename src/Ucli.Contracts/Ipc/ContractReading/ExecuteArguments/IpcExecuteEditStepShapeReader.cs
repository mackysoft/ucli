using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Validates the raw shape of one public <c>kind:"edit"</c> execute step. </summary>
internal static class IpcExecuteEditStepShapeReader
{
    public static bool TryRead (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        return TryReadContext(stepElement, stepIndex, stepId, profile, out error)
            && TryReadSelection(stepElement, stepIndex, stepId, profile, out error)
            && TryReadActions(stepElement, stepIndex, stepId, profile, out error)
            && TryReadCommit(stepElement, stepIndex, stepId, profile, out error)
            && TryValidateFullEditContract(stepElement, stepIndex, stepId, profile, out error);
    }

    private static bool TryReadContext (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        return TryReadObject(
            stepElement,
            "on",
            stepIndex,
            stepId,
            profile,
            IpcExecuteArgumentsContractReadError.StepOnContractViolation,
            out error);
    }

    private static bool TryReadSelection (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        return TryReadObject(
            stepElement,
            "select",
            stepIndex,
            stepId,
            profile,
            IpcExecuteArgumentsContractReadError.StepSelectContractViolation,
            out error);
    }

    private static bool TryReadObject (
        JsonElement stepElement,
        string propertyName,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        Func<int, IpcExecuteStepId?, StepPropertyReadErrorKind, IpcExecuteArgumentsContractReadError> createError,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (IpcExecuteStepPropertyReader.TryReadRequiredObject(stepElement, propertyName, profile.RequireStepObject, out var errorKind))
        {
            error = IpcExecuteArgumentsContractReadError.None;
            return true;
        }

        return TryConvertPropertyError(stepIndex, stepId, profile, errorKind, createError, out error);
    }

    private static bool TryReadActions (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (!IpcExecuteStepPropertyReader.TryReadRequiredArray(stepElement, "actions", profile.RequireStepObject, out var actionsElement, out var errorKind))
        {
            return TryConvertPropertyError(stepIndex, stepId, profile, errorKind, IpcExecuteArgumentsContractReadError.StepActionsContractViolation, out error);
        }

        return IpcExecuteStepPropertyReader.ValidateActionElements(actionsElement, stepIndex, stepId, out error);
    }

    private static bool TryReadCommit (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (!JsonStringContractReader.TryRead(
            stepElement,
            "commit",
            ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out _,
            out var readError))
        {
            error = IpcExecuteArgumentsContractReadError.StepCommitContractViolation(stepIndex, stepId, readError);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryValidateFullEditContract (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (profile.RequireStepObject && !IpcEditStepContractReader.TryRead(stepElement, out _, out var editErrorMessage))
        {
            error = IpcExecuteArgumentsContractReadError.StepEditContractViolation(stepIndex, stepId, editErrorMessage);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryConvertPropertyError (
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        StepPropertyReadErrorKind errorKind,
        Func<int, IpcExecuteStepId?, StepPropertyReadErrorKind, IpcExecuteArgumentsContractReadError> createError,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (profile.RequireStepObject || errorKind == StepPropertyReadErrorKind.TypeMismatch)
        {
            error = createError(stepIndex, stepId, errorKind);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static JsonStringPresenceRequirement ResolveStringPresenceRequirement (bool requireProperty)
    {
        return requireProperty
            ? JsonStringPresenceRequirement.Required
            : JsonStringPresenceRequirement.OptionalStrict;
    }
}
