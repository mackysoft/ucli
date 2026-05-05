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
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            OpResults: opResults,
            Errors: errors,
            Outcome: outcome,
            ReadPostcondition: readPostcondition);
    }
}
