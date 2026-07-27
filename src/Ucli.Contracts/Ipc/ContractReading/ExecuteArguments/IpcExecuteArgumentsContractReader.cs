using System.Globalization;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Reads execute arguments through the authoritative System.Text.Json request contract.</summary>
internal static class IpcExecuteArgumentsContractReader
{
    public static bool TryRead (
        JsonElement argumentsObject,
        out IpcExecuteArgumentsContract argumentsContract,
        out IpcExecuteArgumentsContractReadError error)
    {
        argumentsContract = default!;
        if (argumentsObject.ValueKind != JsonValueKind.Object)
        {
            error = IpcExecuteArgumentsContractReadError.ContractViolation(
                "Request arguments must be a JSON object.");
            return false;
        }

        if (!IpcPayloadCodec.TryDeserializeStrict(
                argumentsObject,
                out IpcExecuteArgumentsJsonContract jsonContract,
                out var payloadError))
        {
            error = IpcExecuteArgumentsContractReadError.ContractViolation(payloadError.Message);
            return false;
        }

        if (!UcliRequestJsonContractValidator.TryValidate(jsonContract, out var invalidStepIndex, out var errorMessage))
        {
            error = IpcExecuteArgumentsContractReadError.ContractViolation(errorMessage, invalidStepIndex);
            return false;
        }

        var steps = new IpcExecuteStepContract[jsonContract.Steps.Count];
        for (var i = 0; i < jsonContract.Steps.Count; i++)
        {
            steps[i] = MapStep(jsonContract.Steps[i], i);
        }

        argumentsContract = new IpcExecuteArgumentsContract(jsonContract.ProtocolVersion, steps);
        error = IpcExecuteArgumentsContractReadError.None;
        return true;
    }

    private static IpcExecuteStepContract MapStep (
        UcliRequestStepJsonContract source,
        int stepIndex)
    {
        var stepId = new IpcExecuteStepId(stepIndex.ToString(CultureInfo.InvariantCulture));
        var element = JsonSerializer.SerializeToElement(
            source,
            typeof(UcliRequestStepJsonContract),
            IpcJsonSerializerOptions.StrictPropertyNames);

        return source switch
        {
            UcliOperationRequestStepJsonContract operation => MapOperation(operation, stepId, element),
            UcliEditRequestStepJsonContract edit => MapEdit(edit, stepId, element),
            _ => throw new InvalidOperationException($"Unsupported request step contract: {source.GetType().FullName}."),
        };
    }

    private static IpcExecuteStepContract MapOperation (
        UcliOperationRequestStepJsonContract operation,
        IpcExecuteStepId stepId,
        JsonElement element)
    {
        return new IpcExecuteStepContract(
            IpcExecuteStepKind.Op,
            stepId,
            operation.Op,
            element)
        {
            OperationArgs = JsonSerializer.SerializeToElement(
                operation.Args,
                IpcJsonSerializerOptions.StrictPropertyNames),
        };
    }

    private static IpcExecuteStepContract MapEdit (
        UcliEditRequestStepJsonContract edit,
        IpcExecuteStepId stepId,
        JsonElement element)
    {
        return new IpcExecuteStepContract(
            IpcExecuteStepKind.Edit,
            stepId,
            OperationName: null,
            element)
        {
            EditContract = UcliRequestExecutionModelMapper.MapEdit(edit, stepId),
        };
    }
}
