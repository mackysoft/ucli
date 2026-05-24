using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Validates the raw shape of one public <c>kind:"edit"</c> request step. </summary>
internal static class IpcRequestEditShapeReader
{
    public static bool TryRead (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
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
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
    {
        return TryReadObject(
            stepElement,
            "on",
            stepIndex,
            stepId,
            profile,
            IpcRequestContractReadError.StepOnContractViolation,
            out error);
    }

    private static bool TryReadSelection (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
    {
        return TryReadObject(
            stepElement,
            "select",
            stepIndex,
            stepId,
            profile,
            IpcRequestContractReadError.StepSelectContractViolation,
            out error);
    }

    private static bool TryReadObject (
        JsonElement stepElement,
        string propertyName,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        Func<int, string?, StepPropertyReadErrorKind, IpcRequestContractReadError> createError,
        out IpcRequestContractReadError error)
    {
        if (IpcRequestStepPropertyReader.TryReadRequiredObject(stepElement, propertyName, profile.RequireStepObject, out var errorKind))
        {
            error = IpcRequestContractReadError.None;
            return true;
        }

        return TryConvertPropertyError(stepIndex, stepId, profile, errorKind, createError, out error);
    }

    private static bool TryReadActions (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
    {
        if (!IpcRequestStepPropertyReader.TryReadRequiredArray(stepElement, "actions", profile.RequireStepObject, out var actionsElement, out var errorKind))
        {
            return TryConvertPropertyError(stepIndex, stepId, profile, errorKind, IpcRequestContractReadError.StepActionsContractViolation, out error);
        }

        return IpcRequestStepPropertyReader.ValidateActionElements(actionsElement, stepIndex, stepId, out error);
    }

    private static bool TryReadCommit (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
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
            error = IpcRequestContractReadError.StepCommitContractViolation(stepIndex, stepId, readError);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryValidateFullEditContract (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
    {
        if (profile.RequireStepObject && !IpcEditStepContractReader.TryRead(stepElement, out _, out var editErrorMessage))
        {
            error = IpcRequestContractReadError.StepEditContractViolation(stepIndex, stepId, editErrorMessage);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryConvertPropertyError (
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        StepPropertyReadErrorKind errorKind,
        Func<int, string?, StepPropertyReadErrorKind, IpcRequestContractReadError> createError,
        out IpcRequestContractReadError error)
    {
        if (profile.RequireStepObject || errorKind == StepPropertyReadErrorKind.TypeMismatch)
        {
            error = createError(stepIndex, stepId, errorKind);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static JsonStringPresenceRequirement ResolveStringPresenceRequirement (bool requireProperty)
    {
        return requireProperty
            ? JsonStringPresenceRequirement.Required
            : JsonStringPresenceRequirement.OptionalStrict;
    }
}
