using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Creates normalized operation-execution results across fixed-operation workflows. </summary>
internal static class OperationExecuteResultFactory
{
    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return FromExecutionError(Guid.NewGuid().ToString("D"), error);
    }

    /// <summary> Creates one failure result from a structured execution error. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromExecutionError (
        string requestId,
        ExecutionError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(error);

        var errorCode = ExecutionErrorCodeMapper.ToCode(error.Kind);
        return Create(
            requestId,
            [],
            [
                new IpcError(errorCode, error.Message, null),
            ],
            error.Kind == ExecutionErrorKind.InvalidArgument
                ? ApplicationOutcome.InvalidArgument
                : ApplicationOutcome.ToolError);
    }

    /// <summary> Creates one failure result from static validation errors. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="validationErrors"> The static validation errors. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromValidationErrors (
        string requestId,
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(validationErrors);

        var errors = new IpcError[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            errors[i] = new IpcError(validationError.Code, validationError.Message, validationError.OpId);
        }

        return Create(
            requestId,
            [],
            errors,
            ApplicationOutcome.InvalidArgument);
    }

    /// <summary> Creates one normalized result from one Unity IPC response. </summary>
    /// <param name="requestId"> The generated request identifier. </param>
    /// <param name="response"> The Unity IPC response. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult FromIpcResponse (
        string requestId,
        IpcResponse response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(response);

        var convertedResponse = ExecuteResponseConverter.Convert(response);
        return Create(
            requestId,
            convertedResponse.OpResults,
            convertedResponse.Errors,
            convertedResponse.Outcome,
            convertedResponse.ReadPostcondition);
    }

    /// <summary> Creates one normalized operation execution result. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="errors"> The machine-readable error list. </param>
    /// <param name="outcome"> The associated application outcome. </param>
    /// <returns> The normalized operation execution result. </returns>
    public static OperationExecuteResult Create (
        string requestId,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        ApplicationOutcome outcome,
        IpcExecuteReadPostcondition? readPostcondition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(opResults);
        ArgumentNullException.ThrowIfNull(errors);

        return new OperationExecuteResult(
            RequestId: requestId,
            OpResults: MapOpResults(opResults),
            Errors: MapErrors(errors),
            Outcome: outcome,
            ReadPostcondition: MapReadPostcondition(readPostcondition));
    }

    private static IReadOnlyList<OperationExecutionOperationResult> MapOpResults (IReadOnlyList<IpcExecuteOperationResult> opResults)
    {
        var mappedResults = new OperationExecutionOperationResult[opResults.Count];
        for (var i = 0; i < opResults.Count; i++)
        {
            var opResult = opResults[i];
            mappedResults[i] = new OperationExecutionOperationResult(
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

        return mappedResults;
    }

    private static IReadOnlyList<OperationExecutionTouchedResource> MapTouchedResources (IReadOnlyList<IpcExecuteTouchedResource> touchedResources)
    {
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

    private static IReadOnlyList<OperationExecutionError> MapErrors (IReadOnlyList<IpcError> errors)
    {
        var mappedErrors = new OperationExecutionError[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            mappedErrors[i] = new OperationExecutionError(
                Code: error.Code,
                Message: error.Message,
                OpId: error.OpId);
        }

        return mappedErrors;
    }

    private static OperationExecutionReadPostcondition? MapReadPostcondition (IpcExecuteReadPostcondition? readPostcondition)
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
}
