using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads the protocol version and steps carried by an <c>execute</c> request's arguments payload. </summary>
internal static class IpcExecuteArgumentsContractReader
{
    private static readonly HashSet<string> AllowedArgumentProperties = new(StringComparer.Ordinal)
    {
        "protocolVersion",
        "steps",
    };

    public static bool TryRead (
        JsonElement argumentsObject,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IpcExecuteArgumentsContract argumentsContract,
        out IpcExecuteArgumentsContractReadError error)
    {
        argumentsContract = default!;
        if (!TryValidateArgumentsObject(argumentsObject, out error))
        {
            return false;
        }

        if (!TryReadProtocolVersion(argumentsObject, profile, out var protocolVersion, out error))
        {
            return false;
        }

        if (!TryReadSteps(argumentsObject, profile, out var steps, out error))
        {
            return false;
        }

        argumentsContract = new IpcExecuteArgumentsContract(
            ProtocolVersion: protocolVersion,
            Steps: steps);
        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryValidateArgumentsObject (
        JsonElement argumentsObject,
        out IpcExecuteArgumentsContractReadError error)
    {
        if (argumentsObject.ValueKind != JsonValueKind.Object)
        {
            error = IpcExecuteArgumentsContractReadError.ArgumentsMustBeObject();
            return false;
        }

        var unknownArgumentsProperty = JsonObjectPropertyReader.FindUnknownProperty(argumentsObject, AllowedArgumentProperties);
        if (unknownArgumentsProperty is not null)
        {
            error = IpcExecuteArgumentsContractReadError.UnknownArgumentsProperty(unknownArgumentsProperty);
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryReadProtocolVersion (
        JsonElement argumentsObject,
        in IpcExecuteArgumentsContractReadProfile profile,
        out int protocolVersion,
        out IpcExecuteArgumentsContractReadError error)
    {
        protocolVersion = 0;
        if (!argumentsObject.TryGetProperty("protocolVersion", out var protocolVersionElement))
        {
            if (profile.RequireProtocolVersion)
            {
                error = IpcExecuteArgumentsContractReadError.ProtocolVersionMissing();
                return false;
            }

            error = IpcExecuteArgumentsContractReadError.None;
            return true;
        }

        if (protocolVersionElement.ValueKind != JsonValueKind.Number
            || !protocolVersionElement.TryGetInt32(out protocolVersion))
        {
            error = IpcExecuteArgumentsContractReadError.ProtocolVersionTypeMismatch();
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryReadSteps (
        JsonElement argumentsObject,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IReadOnlyList<IpcExecuteStepContract?>? steps,
        out IpcExecuteArgumentsContractReadError error)
    {
        steps = null;
        if (!argumentsObject.TryGetProperty("steps", out var stepsElement))
        {
            if (profile.RequireSteps)
            {
                error = IpcExecuteArgumentsContractReadError.StepsMissing();
                return false;
            }

            error = IpcExecuteArgumentsContractReadError.None;
            return true;
        }

        if (stepsElement.ValueKind != JsonValueKind.Array)
        {
            error = IpcExecuteArgumentsContractReadError.StepsTypeMismatch();
            return false;
        }

        if (!TryReadStepArray(stepsElement, profile, out steps, out error))
        {
            return false;
        }

        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static bool TryReadStepArray (
        JsonElement stepsElement,
        in IpcExecuteArgumentsContractReadProfile profile,
        out IReadOnlyList<IpcExecuteStepContract?> steps,
        out IpcExecuteArgumentsContractReadError error)
    {
        var parsedSteps = new List<IpcExecuteStepContract?>();
        var duplicateStepIdDetector = CreateDuplicateStepIdDetector(profile);
        var stepIndex = 0;
        foreach (var stepElement in stepsElement.EnumerateArray())
        {
            if (!IpcExecuteStepElementReader.TryRead(stepElement, stepIndex, profile, duplicateStepIdDetector, out var parsedStep, out error))
            {
                steps = Array.Empty<IpcExecuteStepContract?>();
                return false;
            }

            parsedSteps.Add(parsedStep);
            stepIndex++;
        }

        steps = parsedSteps;
        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static HashSet<string>? CreateDuplicateStepIdDetector (in IpcExecuteArgumentsContractReadProfile profile)
    {
        return profile.RejectDuplicatedStepId
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;
    }

}
