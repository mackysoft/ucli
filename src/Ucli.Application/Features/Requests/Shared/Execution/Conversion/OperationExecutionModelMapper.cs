using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Maps Unity IPC execute contracts into application-owned execution models. </summary>
internal static class OperationExecutionModelMapper
{
    /// <summary> Maps per-operation execute results. </summary>
    public static IReadOnlyList<OperationExecutionOperationResult> MapOpResults (IReadOnlyList<IpcExecuteOperationResult> opResults)
    {
        ArgumentNullException.ThrowIfNull(opResults);

        var mappedResults = new OperationExecutionOperationResult[opResults.Count];
        for (var i = 0; i < opResults.Count; i++)
        {
            mappedResults[i] = MapOpResult(opResults[i]);
        }

        return mappedResults;
    }

    /// <summary> Maps one per-operation execute result. </summary>
    public static OperationExecutionOperationResult MapOpResult (IpcExecuteOperationResult opResult)
    {
        ArgumentNullException.ThrowIfNull(opResult);

        return new OperationExecutionOperationResult(
            OpId: opResult.OpId,
            Op: opResult.Op,
            Phase: opResult.Phase,
            Applied: opResult.Applied,
            Changed: opResult.Changed,
            Touched: MapTouchedResources(opResult.Touched))
        {
            Result = opResult.Result,
        };
    }

    /// <summary> Maps machine-readable execute errors. </summary>
    public static IReadOnlyList<OperationExecutionError> MapErrors (IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var mappedErrors = new OperationExecutionError[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            mappedErrors[i] = MapError(errors[i]);
        }

        return mappedErrors;
    }

    /// <summary> Maps one machine-readable execute error. </summary>
    public static OperationExecutionError MapError (IpcError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new OperationExecutionError(
            Code: error.Code,
            Message: error.Message,
            OpId: error.OpId);
    }

    /// <summary> Maps one optional read-postcondition contract. </summary>
    public static OperationExecutionReadPostcondition? MapReadPostcondition (IpcExecuteReadPostcondition? readPostcondition)
    {
        if (readPostcondition == null)
        {
            return null;
        }

        var requirements = readPostcondition.Requirements;
        var mappedRequirements = new OperationExecutionReadPostconditionRequirement[requirements.Count];
        for (var i = 0; i < requirements.Count; i++)
        {
            var requirement = requirements[i];
            mappedRequirements[i] = new OperationExecutionReadPostconditionRequirement(
                Surface: requirement.Surface,
                MinSafeGeneratedAtUtc: requirement.MinSafeGeneratedAtUtc)
            {
                ScenePath = requirement.ScenePath,
            };
        }

        return new OperationExecutionReadPostcondition(mappedRequirements);
    }

    /// <summary> Creates one plan-phase operation result without exposing IPC DTOs from service results. </summary>
    public static OperationExecutionOperationResult CreatePlanResult (
        string opId,
        string op,
        bool applied,
        bool changed,
        IReadOnlyList<OperationExecutionTouchedResource> touched,
        JsonElement? result = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opId);
        ArgumentException.ThrowIfNullOrWhiteSpace(op);
        ArgumentNullException.ThrowIfNull(touched);

        return new OperationExecutionOperationResult(
            OpId: opId,
            Op: op,
            Phase: IpcExecuteOperationPhaseNames.Plan,
            Applied: applied,
            Changed: changed,
            Touched: touched)
        {
            Result = result,
        };
    }

    private static IReadOnlyList<OperationExecutionTouchedResource> MapTouchedResources (IReadOnlyList<IpcExecuteTouchedResource> touchedResources)
    {
        ArgumentNullException.ThrowIfNull(touchedResources);

        var mappedResources = new OperationExecutionTouchedResource[touchedResources.Count];
        for (var i = 0; i < touchedResources.Count; i++)
        {
            var touchedResource = touchedResources[i];
            mappedResources[i] = new OperationExecutionTouchedResource(
                Kind: touchedResource.Kind,
                Path: touchedResource.Path,
                Guid: touchedResource.Guid);
        }

        return mappedResources;
    }
}
