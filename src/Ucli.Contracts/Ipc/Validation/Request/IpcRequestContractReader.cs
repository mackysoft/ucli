using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides reusable readers for request root and step contracts. </summary>
internal static class IpcRequestContractReader
{
    private static readonly HashSet<string> AllowedRequestProperties = new(StringComparer.Ordinal)
    {
        "protocolVersion",
        "requestId",
        "steps",
    };

    private static readonly HashSet<string> AllowedStepProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "id",
        "op",
        "args",
        "on",
        "select",
        "actions",
        "commit",
    };

    private static readonly HashSet<string> AllowedOpStepProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "id",
        "op",
        "args",
    };

    private static readonly HashSet<string> AllowedEditStepProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "id",
        "on",
        "select",
        "actions",
        "commit",
    };

    public static bool TryRead (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContract requestContract,
        out IpcRequestContractReadError error)
    {
        requestContract = default!;
        if (requestObject.ValueKind != JsonValueKind.Object)
        {
            error = IpcRequestContractReadError.RequestMustBeObject();
            return false;
        }

        var unknownRequestProperty = JsonPropertyGuard.FindUnknownProperty(requestObject, AllowedRequestProperties);
        if (unknownRequestProperty is not null)
        {
            error = IpcRequestContractReadError.UnknownRequestProperty(unknownRequestProperty);
            return false;
        }

        if (!TryReadProtocolVersion(requestObject, profile, out var protocolVersion, out error))
        {
            return false;
        }

        if (!TryReadRequestId(requestObject, profile, out var requestId, out error))
        {
            return false;
        }

        if (!TryReadSteps(requestObject, profile, out var steps, out error))
        {
            return false;
        }

        requestContract = new IpcRequestContract(
            ProtocolVersion: protocolVersion,
            RequestId: requestId,
            Steps: steps);
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadProtocolVersion (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out int protocolVersion,
        out IpcRequestContractReadError error)
    {
        protocolVersion = 0;
        if (!requestObject.TryGetProperty("protocolVersion", out var protocolVersionElement))
        {
            if (profile.RequireProtocolVersion)
            {
                error = IpcRequestContractReadError.ProtocolVersionMissing();
                return false;
            }

            error = IpcRequestContractReadError.None;
            return true;
        }

        if (protocolVersionElement.ValueKind != JsonValueKind.Number
            || !protocolVersionElement.TryGetInt32(out protocolVersion))
        {
            error = IpcRequestContractReadError.ProtocolVersionTypeMismatch();
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadRequestId (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out string? requestId,
        out IpcRequestContractReadError error)
    {
        if (!JsonStringContractReader.TryRead(
            jsonObject: requestObject,
            propertyName: "requestId",
            presenceRequirement: ResolveStringPresenceRequirement(profile.RequireRequestId),
            rejectEmptyOrWhitespace: profile.RequireNonEmptyRequestId,
            rejectOuterWhitespace: profile.RejectRequestIdOuterWhitespace,
            value: out requestId,
            error: out var readError))
        {
            error = IpcRequestContractReadError.RequestIdContractViolation(readError);
            return false;
        }

        if (!profile.RequireCanonicalRequestIdFormat || requestId is null)
        {
            error = IpcRequestContractReadError.None;
            return true;
        }

        if (!Guid.TryParseExact(requestId, "D", out var parsedRequestId))
        {
            error = IpcRequestContractReadError.RequestIdFormatMismatch();
            return false;
        }

        requestId = parsedRequestId.ToString("D");
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadSteps (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out IReadOnlyList<IpcRequestContractStep?>? steps,
        out IpcRequestContractReadError error)
    {
        steps = null;
        if (!requestObject.TryGetProperty("steps", out var stepsElement))
        {
            if (profile.RequireSteps)
            {
                error = IpcRequestContractReadError.StepsMissing();
                return false;
            }

            error = IpcRequestContractReadError.None;
            return true;
        }

        if (stepsElement.ValueKind != JsonValueKind.Array)
        {
            error = IpcRequestContractReadError.StepsTypeMismatch();
            return false;
        }

        var parsedSteps = new List<IpcRequestContractStep?>();
        HashSet<string>? duplicateStepIdDetector = profile.RejectDuplicatedStepId
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;

        var stepIndex = 0;
        foreach (var stepElement in stepsElement.EnumerateArray())
        {
            if (!TryReadStepElement(
                stepElement,
                stepIndex,
                profile,
                duplicateStepIdDetector,
                out var parsedStep,
                out error))
            {
                steps = null;
                return false;
            }

            parsedSteps.Add(parsedStep);
            stepIndex++;
        }

        steps = parsedSteps;
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadStepElement (
        JsonElement stepElement,
        int stepIndex,
        in IpcRequestContractReadProfile profile,
        HashSet<string>? duplicateStepIdDetector,
        out IpcRequestContractStep? step,
        out IpcRequestContractReadError error)
    {
        step = null;
        if (stepElement.ValueKind != JsonValueKind.Object)
        {
            if (profile.RequireStepObject)
            {
                error = IpcRequestContractReadError.StepMustBeObject(stepIndex);
                return false;
            }

            error = IpcRequestContractReadError.None;
            return true;
        }

        if (!TryReadStepKind(stepElement, stepIndex, profile, out var stepKind, out error))
        {
            return false;
        }

        var allowedProperties = ResolveAllowedStepProperties(stepKind);
        var unknownStepProperty = JsonPropertyGuard.FindUnknownProperty(stepElement, allowedProperties);
        if (unknownStepProperty is not null)
        {
            error = IpcRequestContractReadError.UnknownStepProperty(stepIndex, unknownStepProperty);
            return false;
        }

        if (!TryReadStepId(stepElement, stepIndex, profile, out var stepId, out error))
        {
            return false;
        }

        string? operationName = null;
        if (stepKind == IpcRequestStepKind.Op)
        {
            if (!TryReadStepOp(stepElement, stepIndex, stepId, profile, out operationName, out error))
            {
                return false;
            }
        }
        else if (stepKind == IpcRequestStepKind.Edit && !TryReadEditShape(stepElement, stepIndex, stepId, profile, out error))
        {
            return false;
        }

        if (stepId is not null
            && duplicateStepIdDetector is not null
            && !duplicateStepIdDetector.Add(stepId))
        {
            error = IpcRequestContractReadError.DuplicatedStepIdError(stepIndex, stepId);
            return false;
        }

        step = new IpcRequestContractStep(
            Kind: stepKind,
            Id: stepId,
            OperationName: operationName,
            Element: stepElement.Clone());
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
        if (!JsonStringContractReader.TryRead(
            jsonObject: stepElement,
            propertyName: "kind",
            presenceRequirement: ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            value: out var kindLiteral,
            error: out var readError))
        {
            error = IpcRequestContractReadError.StepKindContractViolation(stepIndex, readError);
            return false;
        }

        if (kindLiteral is null)
        {
            error = IpcRequestContractReadError.None;
            return true;
        }

        if (string.Equals(kindLiteral, "op", StringComparison.Ordinal))
        {
            stepKind = IpcRequestStepKind.Op;
            error = IpcRequestContractReadError.None;
            return true;
        }

        if (string.Equals(kindLiteral, "edit", StringComparison.Ordinal))
        {
            stepKind = IpcRequestStepKind.Edit;
            error = IpcRequestContractReadError.None;
            return true;
        }

        error = IpcRequestContractReadError.StepKindUnsupported(stepIndex, kindLiteral);
        return false;
    }

    private static bool TryReadStepId (
        JsonElement stepElement,
        int stepIndex,
        in IpcRequestContractReadProfile profile,
        out string? stepId,
        out IpcRequestContractReadError error)
    {
        if (!JsonStringContractReader.TryRead(
            jsonObject: stepElement,
            propertyName: "id",
            presenceRequirement: ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            value: out stepId,
            error: out var readError))
        {
            error = IpcRequestContractReadError.StepIdContractViolation(stepIndex, readError);
            return false;
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
        if (!JsonStringContractReader.TryRead(
            jsonObject: stepElement,
            propertyName: "op",
            presenceRequirement: ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            value: out operationName,
            error: out var readError))
        {
            error = IpcRequestContractReadError.StepOpContractViolation(stepIndex, stepId, readError);
            return false;
        }

        if (!TryReadRequiredObject(stepElement, "args", profile.RequireStepObject, out _, out var propertyErrorKind))
        {
            if (profile.RequireStepObject || propertyErrorKind == StepPropertyReadErrorKind.TypeMismatch)
            {
                error = IpcRequestContractReadError.StepArgsContractViolation(stepIndex, stepId, propertyErrorKind);
                return false;
            }
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadEditShape (
        JsonElement stepElement,
        int stepIndex,
        string? stepId,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContractReadError error)
    {
        if (!TryReadRequiredObject(stepElement, "on", profile.RequireStepObject, out _, out var onErrorKind))
        {
            if (profile.RequireStepObject || onErrorKind == StepPropertyReadErrorKind.TypeMismatch)
            {
                error = IpcRequestContractReadError.StepOnContractViolation(stepIndex, stepId, onErrorKind);
                return false;
            }
        }

        if (!TryReadRequiredObject(stepElement, "select", profile.RequireStepObject, out _, out var selectErrorKind))
        {
            if (profile.RequireStepObject || selectErrorKind == StepPropertyReadErrorKind.TypeMismatch)
            {
                error = IpcRequestContractReadError.StepSelectContractViolation(stepIndex, stepId, selectErrorKind);
                return false;
            }
        }

        if (!TryReadRequiredArray(stepElement, "actions", profile.RequireStepObject, out var actionsElement, out var actionsErrorKind))
        {
            if (profile.RequireStepObject || actionsErrorKind == StepPropertyReadErrorKind.TypeMismatch)
            {
                error = IpcRequestContractReadError.StepActionsContractViolation(stepIndex, stepId, actionsErrorKind);
                return false;
            }
        }
        else if (!ValidateActionElements(actionsElement, stepIndex, stepId, out error))
        {
            return false;
        }

        if (!JsonStringContractReader.TryRead(
            jsonObject: stepElement,
            propertyName: "commit",
            presenceRequirement: ResolveStringPresenceRequirement(profile.RequireStepObject),
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            value: out _,
            error: out var readError))
        {
            error = IpcRequestContractReadError.StepCommitContractViolation(stepIndex, stepId, readError);
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool ValidateActionElements (
        JsonElement actionsElement,
        int stepIndex,
        string? stepId,
        out IpcRequestContractReadError error)
    {
        foreach (var actionElement in actionsElement.EnumerateArray())
        {
            if (actionElement.ValueKind != JsonValueKind.Object)
            {
                error = IpcRequestContractReadError.StepActionMustBeObject(stepIndex, stepId);
                return false;
            }
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static HashSet<string> ResolveAllowedStepProperties (IpcRequestStepKind? stepKind)
    {
        if (stepKind == IpcRequestStepKind.Op)
        {
            return AllowedOpStepProperties;
        }

        if (stepKind == IpcRequestStepKind.Edit)
        {
            return AllowedEditStepProperties;
        }

        return AllowedStepProperties;
    }

    private static bool TryReadRequiredObject (
        JsonElement jsonObject,
        string propertyName,
        bool requireProperty,
        out JsonElement propertyElement,
        out StepPropertyReadErrorKind errorKind)
    {
        propertyElement = default;
        if (!jsonObject.TryGetProperty(propertyName, out propertyElement))
        {
            errorKind = requireProperty
                ? StepPropertyReadErrorKind.Missing
                : StepPropertyReadErrorKind.None;
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.Object)
        {
            errorKind = StepPropertyReadErrorKind.TypeMismatch;
            return false;
        }

        errorKind = StepPropertyReadErrorKind.None;
        return true;
    }

    private static bool TryReadRequiredArray (
        JsonElement jsonObject,
        string propertyName,
        bool requireProperty,
        out JsonElement propertyElement,
        out StepPropertyReadErrorKind errorKind)
    {
        propertyElement = default;
        if (!jsonObject.TryGetProperty(propertyName, out propertyElement))
        {
            errorKind = requireProperty
                ? StepPropertyReadErrorKind.Missing
                : StepPropertyReadErrorKind.None;
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.Array)
        {
            errorKind = StepPropertyReadErrorKind.TypeMismatch;
            return false;
        }

        errorKind = StepPropertyReadErrorKind.None;
        return true;
    }

    private static JsonStringPresenceRequirement ResolveStringPresenceRequirement (bool requireProperty)
    {
        return requireProperty
            ? JsonStringPresenceRequirement.Required
            : JsonStringPresenceRequirement.OptionalStrict;
    }
}