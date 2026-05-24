using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads one public request step element. </summary>
internal static class IpcRequestStepElementReader
{
    public static bool TryRead (
        JsonElement stepElement,
        int stepIndex,
        in IpcRequestContractReadProfile profile,
        HashSet<string>? duplicateStepIdDetector,
        out IpcRequestContractStep? step,
        out IpcRequestContractReadError error)
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
        in IpcRequestContractReadProfile profile,
        HashSet<string>? duplicateStepIdDetector,
        out IpcRequestContractStep? step,
        out IpcRequestContractReadError error)
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

        step = new IpcRequestContractStep(stepKind, stepId, operationName, stepElement.Clone());
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryHandleNonObjectStep (
        JsonElement stepElement,
        int stepIndex,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
    {
        if (stepElement.ValueKind == JsonValueKind.Object)
        {
            error = IpcRequestContractReadError.None;
            return true;
        }

        error = profile.RequireStepObject
            ? IpcRequestContractReadError.StepMustBeObject(stepIndex)
            : IpcRequestContractReadError.None;
        return !profile.RequireStepObject;
    }

    private static bool TryValidateAllowedProperties (
        JsonElement stepElement,
        int stepIndex,
        IpcRequestStepKind? stepKind,
        out IpcRequestContractReadError error)
    {
        var allowedProperties = IpcRequestStepPropertyPolicy.ResolveAllowedStepProperties(stepKind);
        var unknownStepProperty = JsonObjectPropertyReader.FindUnknownProperty(stepElement, allowedProperties);
        if (unknownStepProperty is not null)
        {
            error = IpcRequestContractReadError.UnknownStepProperty(stepIndex, unknownStepProperty);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadStepKind (
        JsonElement stepElement,
        int stepIndex,
        in IpcRequestContractReadProfile profile,
        out IpcRequestStepKind? stepKind,
        out IpcRequestContractReadError error)
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
        in IpcRequestContractReadProfile profile,
        out string? kindLiteral,
        out IpcRequestContractReadError error)
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
            error = IpcRequestContractReadError.StepKindContractViolation(stepIndex, readError);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryParseStepKind (
        string? kindLiteral,
        int stepIndex,
        out IpcRequestStepKind? stepKind,
        out IpcRequestContractReadError error)
    {
        stepKind = kindLiteral switch
        {
            null => null,
            "op" => IpcRequestStepKind.Op,
            "edit" => IpcRequestStepKind.Edit,
            _ => null,
        };
        error = stepKind is null && kindLiteral is not null
            ? IpcRequestContractReadError.StepKindUnsupported(stepIndex, kindLiteral)
            : IpcRequestContractReadError.None;
        return kindLiteral is null || stepKind is not null;
    }

    private static bool TryReadStepId (
        JsonElement stepElement,
        int stepIndex,
        in IpcRequestContractReadProfile profile,
        out string? stepId,
        out IpcRequestContractReadError error)
    {
        if (!JsonStringContractReader.TryRead(
            stepElement,
            "id",
            ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out stepId,
            out var readError))
        {
            error = IpcRequestContractReadError.StepIdContractViolation(stepIndex, readError);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadStepShape (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        IpcRequestStepKind? stepKind,
        in IpcRequestContractReadProfile profile,
        out string? operationName,
        out IpcRequestContractReadError error)
    {
        operationName = null;
        if (stepKind == IpcRequestStepKind.Op)
        {
            return TryReadStepOp(stepElement, stepIndex, stepId, profile, out operationName, out error);
        }

        if (stepKind == IpcRequestStepKind.Edit)
        {
            return IpcRequestEditShapeReader.TryRead(stepElement, stepIndex, stepId, profile, out error);
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadStepOp (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out string? operationName,
        out IpcRequestContractReadError error)
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
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out string? operationName,
        out IpcRequestContractReadError error)
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
            error = IpcRequestContractReadError.StepOpContractViolation(stepIndex, stepId, readError);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryValidateArgsObject (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
    {
        if (IpcRequestStepPropertyReader.TryReadRequiredObject(stepElement, "args", profile.RequireStepObject, out var propertyErrorKind))
        {
            error = IpcRequestContractReadError.None;
            return true;
        }

        return TryConvertArgsError(stepIndex, stepId, profile, propertyErrorKind, out error);
    }

    private static bool TryConvertArgsError (
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        StepPropertyReadErrorKind propertyErrorKind,
        out IpcRequestContractReadError error)
    {
        if (profile.RequireStepObject || propertyErrorKind == StepPropertyReadErrorKind.TypeMismatch)
        {
            error = IpcRequestContractReadError.StepArgsContractViolation(stepIndex, stepId, propertyErrorKind);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryTrackStepId (
        int stepIndex,
        string? stepId,
        HashSet<string>? duplicateStepIdDetector,
        out IpcRequestContractReadError error)
    {
        if (stepId is not null
            && duplicateStepIdDetector is not null
            && !duplicateStepIdDetector.Add(stepId))
        {
            error = IpcRequestContractReadError.DuplicatedStepIdError(stepIndex, stepId);
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
