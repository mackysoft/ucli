using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads one public <c>execute</c> step element. </summary>
internal static class IpcExecuteStepElementReader
{
    public static bool TryRead (
        JsonElement stepElement,
        int stepIndex,
        in IpcExecuteArgumentsContractReadProfile profile,
        HashSet<IpcExecuteStepId>? duplicateStepIdDetector,
        out IpcExecuteStepContract? step,
        out IpcExecuteArgumentsContractReadError error)
    {
        step = null;
        if (!TryHandleNonObjectStep(stepElement, stepIndex, profile, out error))
        {
            return false;
        }

        if (stepElement.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        return TryReadObjectStep(stepElement, stepIndex, profile, duplicateStepIdDetector, out step, out error);
    }

    private static bool TryReadObjectStep (
        JsonElement stepElement,
        int stepIndex,
        in IpcExecuteArgumentsContractReadProfile profile,
        HashSet<IpcExecuteStepId>? duplicateStepIdDetector,
        out IpcExecuteStepContract? step,
        out IpcExecuteArgumentsContractReadError error)
    {
        step = null;
        if (!TryReadStepKind(stepElement, stepIndex, profile, out var stepKind, out error)
            || !TryValidateAllowedProperties(stepElement, stepIndex, stepKind, out error)
            || !TryReadStepId(stepElement, stepIndex, profile, out var stepId, out error)
            || !TryReadStepShape(stepElement, stepIndex, stepId, stepKind, profile, out var operationName, out error)
            || !TryTrackStepId(stepIndex, stepId, duplicateStepIdDetector, out error))
        {
            return false;
        }

        step = new IpcExecuteStepContract(stepKind, stepId, operationName, stepElement.Clone());
        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryHandleNonObjectStep (
        JsonElement stepElement,
        int stepIndex,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (stepElement.ValueKind == JsonValueKind.Object)
        {
            error = IpcExecuteArgumentsContractReadError.None;
            return true;
        }

        error = profile.RequireStepObject
            ? IpcExecuteArgumentsContractReadError.StepMustBeObject(stepIndex)
            : IpcExecuteArgumentsContractReadError.None;
        return !profile.RequireStepObject;
    }

    private static bool TryValidateAllowedProperties (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepKind? stepKind,
        out IpcExecuteArgumentsContractReadError error)
    {
        var allowedProperties = IpcExecuteStepPropertyPolicy.ResolveAllowedStepProperties(stepKind);
        var unknownStepProperty = JsonObjectPropertyReader.FindUnknownProperty(stepElement, allowedProperties);
        if (unknownStepProperty is not null)
        {
            error = IpcExecuteArgumentsContractReadError.UnknownStepProperty(stepIndex, unknownStepProperty);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryReadStepKind (
        JsonElement stepElement,
        int stepIndex,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteStepKind? stepKind,
        out IpcExecuteArgumentsContractReadError error)
    {
        stepKind = null;
        if (!TryReadStepKindLiteral(stepElement, stepIndex, profile, out var kindLiteral, out error))
        {
            return false;
        }

        return TryParseStepKind(kindLiteral, stepIndex, out stepKind, out error);
    }

    private static bool TryReadStepKindLiteral (
        JsonElement stepElement,
        int stepIndex,
        in IpcExecuteArgumentsContractReadProfile profile,
        out string? kindLiteral,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (!JsonStringContractReader.TryRead(
            stepElement,
            "kind",
            ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out kindLiteral,
            out var readError))
        {
            error = IpcExecuteArgumentsContractReadError.StepKindContractViolation(stepIndex, readError);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryParseStepKind (
        string? kindLiteral,
        int stepIndex,
        out IpcExecuteStepKind? stepKind,
        out IpcExecuteArgumentsContractReadError error)
    {
        stepKind = kindLiteral switch
        {
            null => null,
            "op" => IpcExecuteStepKind.Op,
            "edit" => IpcExecuteStepKind.Edit,
            _ => null,
        };
        error = stepKind is null && kindLiteral is not null
            ? IpcExecuteArgumentsContractReadError.StepKindUnsupported(stepIndex, kindLiteral)
            : IpcExecuteArgumentsContractReadError.None;
        return kindLiteral is null || stepKind is not null;
    }

    private static bool TryReadStepId (
        JsonElement stepElement,
        int stepIndex,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteStepId? stepId,
        out IpcExecuteArgumentsContractReadError error)
    {
        stepId = null;
        if (!JsonStringContractReader.TryRead(
            stepElement,
            "id",
            ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out var stepIdValue,
            out var readError))
        {
            error = IpcExecuteArgumentsContractReadError.StepIdContractViolation(stepIndex, readError);
            return false;
        }

        stepId = stepIdValue == null
            ? null
            : new IpcExecuteStepId(stepIdValue);
        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryReadStepShape (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        IpcExecuteStepKind? stepKind,
        in IpcExecuteArgumentsContractReadProfile profile,
        out string? operationName,
        out IpcExecuteArgumentsContractReadError error)
    {
        operationName = null;
        if (stepKind == IpcExecuteStepKind.Op)
        {
            return TryReadStepOp(stepElement, stepIndex, stepId, profile, out operationName, out error);
        }

        if (stepKind == IpcExecuteStepKind.Edit)
        {
            return IpcExecuteEditStepShapeReader.TryRead(stepElement, stepIndex, stepId, profile, out error);
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryReadStepOp (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out string? operationName,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (!TryReadOperationName(stepElement, stepIndex, stepId, profile, out operationName, out error))
        {
            return false;
        }

        return TryValidateArgsObject(stepElement, stepIndex, stepId, profile, out error);
    }

    private static bool TryReadOperationName (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out string? operationName,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (!JsonStringContractReader.TryRead(
            stepElement,
            "op",
            ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out operationName,
            out var readError))
        {
            error = IpcExecuteArgumentsContractReadError.StepOpContractViolation(stepIndex, stepId, readError);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryValidateArgsObject (
        JsonElement stepElement,
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (IpcExecuteStepPropertyReader.TryReadRequiredObject(stepElement, "args", profile.RequireStepObject, out var propertyErrorKind))
        {
            error = IpcExecuteArgumentsContractReadError.None;
            return true;
        }

        return TryConvertArgsError(stepIndex, stepId, profile, propertyErrorKind, out error);
    }

    private static bool TryConvertArgsError (
        int stepIndex,
        IpcExecuteStepId? stepId,
        in IpcExecuteArgumentsContractReadProfile profile,
        StepPropertyReadErrorKind propertyErrorKind,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (profile.RequireStepObject || propertyErrorKind == StepPropertyReadErrorKind.TypeMismatch)
        {
            error = IpcExecuteArgumentsContractReadError.StepArgsContractViolation(stepIndex, stepId, propertyErrorKind);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryTrackStepId (
        int stepIndex,
        IpcExecuteStepId? stepId,
        HashSet<IpcExecuteStepId>? duplicateStepIdDetector,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (stepId is not null
            && duplicateStepIdDetector is not null
            && !duplicateStepIdDetector.Add(stepId))
        {
            error = IpcExecuteArgumentsContractReadError.DuplicatedStepIdError(stepIndex, stepId);
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
