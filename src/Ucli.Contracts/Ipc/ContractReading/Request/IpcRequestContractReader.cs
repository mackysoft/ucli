using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Provides reusable readers for request root and step contracts. </summary>
internal static class IpcRequestContractReader
{
    private static readonly HashSet<string> AllowedRequestProperties = new(StringComparer.Ordinal)
    {
        "protocolVersion",
        "requestId",
        "steps",
    };

    public static bool TryRead (
        JsonElement requestObject,
        in IpcRequestContractReadProfile profile,
        out IpcRequestContract requestContract,
        out IpcRequestContractReadError error)
    {
        requestContract = default!;
        if (!TryValidateRequestObject(requestObject, out error))
        {
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

    private static bool TryValidateRequestObject (
        JsonElement requestObject,
        out IpcRequestContractReadError error)
    {
        if (requestObject.ValueKind != JsonValueKind.Object)
        {
            error = IpcRequestContractReadError.RequestMustBeObject();
            return false;
        }

        var unknownRequestProperty = JsonObjectPropertyReader.FindUnknownProperty(requestObject, AllowedRequestProperties);
        if (unknownRequestProperty is not null)
        {
            error = IpcRequestContractReadError.UnknownRequestProperty(unknownRequestProperty);
            return false;
        }

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

        if (!TryReadStepArray(stepsElement, profile, out steps, out error))
        {
            return false;
        }

        error = IpcRequestContractReadError.None;
        return true;
    }

    private static bool TryReadStepArray (
        JsonElement stepsElement,
        in IpcRequestContractReadProfile profile,
        out IReadOnlyList<IpcRequestContractStep?> steps,
        out IpcRequestContractReadError error)
    {
        var parsedSteps = new List<IpcRequestContractStep?>();
        var duplicateStepIdDetector = CreateDuplicateStepIdDetector(profile);
        var stepIndex = 0;
        foreach (var stepElement in stepsElement.EnumerateArray())
        {
            if (!IpcRequestStepElementReader.TryRead(stepElement, stepIndex, profile, duplicateStepIdDetector, out var parsedStep, out error))
            {
                steps = Array.Empty<IpcRequestContractStep?>();
                return false;
            }

            parsedSteps.Add(parsedStep);
            stepIndex++;
        }

        steps = parsedSteps;
        error = IpcRequestContractReadError.None;
        return true;
    }

    private static HashSet<string>? CreateDuplicateStepIdDetector (in IpcRequestContractReadProfile profile)
    {
        return profile.RejectDuplicatedStepId
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;
    }

    private static JsonStringPresenceRequirement ResolveStringPresenceRequirement (bool requireProperty)
    {
        return requireProperty
            ? JsonStringPresenceRequirement.Required
            : JsonStringPresenceRequirement.OptionalStrict;
    }
}
