using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;

/// <summary> Aligns Unity operation results with the request steps and direct dispatch that produced them. </summary>
internal static class OperationExecutionResultContractValidator
{
    /// <summary> Validates the operation results carried by one plan or call pass response. </summary>
    /// <param name="request"> The prepared request whose public steps produced the results. </param>
    /// <param name="operationsByName"> The fixed operation descriptors used during request preparation. </param>
    /// <param name="executedPass"> The plan or call pass that produced the response. </param>
    /// <param name="response"> The converted Unity response. </param>
    /// <param name="errorMessage"> The trust-boundary failure message on failure; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the response can be accepted. </returns>
    public static bool TryValidate (
        ValidateRequest request,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName,
        IpcExecuteOperationPhase executedPass,
        ExecuteResponseConversionResult response,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operationsByName);
        ArgumentNullException.ThrowIfNull(response);
        ThrowIfUnsupportedPass(executedPass);
        if (!TryValidateStepResultCount(request.Steps.Count, response, out errorMessage))
        {
            return false;
        }

        return TryValidateStepResults(
            request,
            operationsByName,
            executedPass,
            response,
            out errorMessage);
    }

    /// <summary> Validates one direct operation response against the descriptor used to dispatch it. </summary>
    /// <param name="operationDescriptor"> The fixed operation descriptor used for dispatch. </param>
    /// <param name="executedPass"> The plan or call pass that produced the response. </param>
    /// <param name="response"> The converted Unity response. </param>
    /// <param name="errorMessage"> The trust-boundary failure message on failure; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the response can be accepted. </returns>
    public static bool TryValidateDirectOperation (
        UcliOperationDescriptor operationDescriptor,
        IpcExecuteOperationPhase executedPass,
        ExecuteResponseConversionResult response,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(operationDescriptor);
        ArgumentNullException.ThrowIfNull(response);
        ThrowIfUnsupportedPass(executedPass);
        if (!TryValidateDirectResultCount(response, out errorMessage))
        {
            return false;
        }

        if (response.OpResults.Count == 0)
        {
            errorMessage = null;
            return true;
        }

        return TryValidateDirectResult(
            operationDescriptor,
            executedPass,
            response,
            out errorMessage);
    }

    private static bool TryValidateStepResults (
        ValidateRequest request,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName,
        IpcExecuteOperationPhase executedPass,
        ExecuteResponseConversionResult response,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        var context = new ResponseValidationContext(
            operationsByName,
            executedPass,
            new RegisteredOperationResultContractValidator(
                executedPass,
                response));
        for (var resultIndex = 0; resultIndex < response.OpResults.Count; resultIndex++)
        {
            if (!TryValidateStepResult(
                    resultIndex,
                    request.Steps[resultIndex],
                    response.OpResults[resultIndex],
                    context,
                    out errorMessage))
            {
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    private static bool TryValidateDirectResult (
        UcliOperationDescriptor operationDescriptor,
        IpcExecuteOperationPhase executedPass,
        ExecuteResponseConversionResult response,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        var result = response.OpResults[0];
        if (!string.Equals(result.Op, operationDescriptor.Name, StringComparison.Ordinal))
        {
            errorMessage = "Execute response payload is invalid. The 'opResults[0].op' field does not match the direct operation request.";
            return false;
        }

        var resultValidator = new RegisteredOperationResultContractValidator(
            executedPass,
            response);
        return resultValidator.TryValidate(
            resultIndex: 0,
            operationDescriptor,
            result,
            out errorMessage);
    }

    private static bool TryValidateStepResult (
        int resultIndex,
        ValidateRequestStep step,
        OperationExecutionOperationResult result,
        ResponseValidationContext context,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        var expectedOperationName = step.Kind == IpcExecuteStepKind.Edit
            ? TextVocabulary.GetText(step.Kind)
            : step.Op;
        if (expectedOperationName == null
            || !string.Equals(result.Op, expectedOperationName, StringComparison.Ordinal))
        {
            errorMessage = $"Execute response payload is invalid. The 'opResults[{resultIndex}].op' field does not match request step {resultIndex}.";
            return false;
        }

        return step.Kind == IpcExecuteStepKind.Edit
            ? TryValidateEditResult(resultIndex, result, context, out errorMessage)
            : TryValidateRegisteredResult(resultIndex, expectedOperationName, result, context, out errorMessage);
    }

    private static bool TryValidateRegisteredResult (
        int resultIndex,
        string expectedOperationName,
        OperationExecutionOperationResult result,
        ResponseValidationContext context,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (!context.RegisteredResultValidator.TryValidatePhase(
                resultIndex,
                result,
                out errorMessage))
        {
            return false;
        }

        if (!context.OperationsByName.TryGetValue(expectedOperationName, out var operationDescriptor))
        {
            errorMessage = $"Execute response payload is invalid. Operation '{expectedOperationName}' no longer has the descriptor used to prepare the request.";
            return false;
        }

        return context.RegisteredResultValidator.TryValidatePreparedContract(
            resultIndex,
            operationDescriptor,
            result,
            out errorMessage);
    }

    private static bool TryValidateEditResult (
        int resultIndex,
        OperationExecutionOperationResult result,
        ResponseValidationContext context,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (result.Verdict != null)
        {
            errorMessage = $"Execute response payload is invalid. The 'opResults[{resultIndex}].verdict' field must be null for an Edit step.";
            return false;
        }

        if (!context.RegisteredResultValidator.TryValidatePhase(
                resultIndex,
                result,
                out errorMessage))
        {
            return false;
        }

        return TryValidateEditPayload(resultIndex, result, out errorMessage);
    }

    private static bool TryValidateEditPayload (
        int resultIndex,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (result.OperationDescriptorDigest != null)
        {
            errorMessage = $"Execute response payload is invalid. The 'opResults[{resultIndex}].operationDescriptorDigest' field must be null for an Edit step.";
            return false;
        }

        if (result.Result != null)
        {
            errorMessage = $"Execute response payload is invalid. The 'opResults[{resultIndex}].result' field must be null for an Edit step.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryValidateStepResultCount (
        int requestStepCount,
        ExecuteResponseConversionResult response,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        var resultCount = response.OpResults.Count;
        if (response.IsSuccess
            ? resultCount == requestStepCount
            : resultCount <= requestStepCount)
        {
            errorMessage = null;
            return true;
        }

        errorMessage = response.IsSuccess
            ? $"Execute response payload is invalid. The 'opResults' field contains {resultCount} items, but the request contains {requestStepCount} steps."
            : $"Execute response payload is invalid. The partial 'opResults' field contains {resultCount} items, but the request contains only {requestStepCount} steps.";
        return false;
    }

    private static bool TryValidateDirectResultCount (
        ExecuteResponseConversionResult response,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (response.OpResults.Count <= 1
            && (!response.IsSuccess || response.OpResults.Count == 1))
        {
            errorMessage = null;
            return true;
        }

        errorMessage = response.IsSuccess
            ? $"Execute response payload is invalid. The 'opResults' field contains {response.OpResults.Count} items, but the direct operation request contains one step."
            : "Execute response payload is invalid. The partial 'opResults' field contains more than one item for a direct operation request.";
        return false;
    }

    private static void ThrowIfUnsupportedPass (IpcExecuteOperationPhase executedPass)
    {
        if (executedPass is not IpcExecuteOperationPhase.Plan and not IpcExecuteOperationPhase.Call)
        {
            throw new ArgumentOutOfRangeException(
                nameof(executedPass),
                executedPass,
                "Only plan and call pass responses can be validated.");
        }
    }

    private sealed record ResponseValidationContext (
        IReadOnlyDictionary<string, UcliOperationDescriptor> OperationsByName,
        IpcExecuteOperationPhase ExecutedPass,
        RegisteredOperationResultContractValidator RegisteredResultValidator);
}
