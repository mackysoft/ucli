using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;

/// <summary> Validates one registered Operation result against its executed phase and prepared descriptor. </summary>
internal sealed class RegisteredOperationResultContractValidator
{
    private readonly IpcExecuteOperationPhase executedPass;
    private readonly ExecuteResponseConversionResult response;

    /// <summary> Initializes a validator bound to one converted execution response. </summary>
    /// <param name="executedPass"> The plan or call pass that produced the response. </param>
    /// <param name="response"> The complete converted response that owns success and error facts. </param>
    public RegisteredOperationResultContractValidator (
        IpcExecuteOperationPhase executedPass,
        ExecuteResponseConversionResult response)
    {
        this.executedPass = executedPass;
        this.response = response ?? throw new ArgumentNullException(nameof(response));
    }

    /// <summary> Validates one registered result and its optional result value and verdict. </summary>
    /// <param name="resultIndex"> The result index aligned with its request step. </param>
    /// <param name="operationDescriptor"> The exact descriptor used to prepare and dispatch the operation. </param>
    /// <param name="result"> The normalized operation result. </param>
    /// <param name="errorMessage"> The trust-boundary failure message on failure; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the registered result can be accepted. </returns>
    public bool TryValidate (
        int resultIndex,
        UcliOperationDescriptor operationDescriptor,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(operationDescriptor);
        ArgumentNullException.ThrowIfNull(result);
        if (!TryValidatePhase(resultIndex, result, out errorMessage))
        {
            return false;
        }

        return TryValidatePreparedContract(
            resultIndex,
            operationDescriptor,
            result,
            out errorMessage);
    }

    /// <summary> Validates the reached phase for one registered result. </summary>
    public bool TryValidatePhase (
        int resultIndex,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        var completion = ClassifyCompletion(resultIndex, result);
        if (completion == OperationResultCompletion.Completed
            && result.Phase != executedPass)
        {
            errorMessage = $"Execute response payload is invalid. The 'opResults[{resultIndex}].phase' field does not match the executed pass.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary> Validates one phase-accepted result against its prepared descriptor. </summary>
    public bool TryValidatePreparedContract (
        int resultIndex,
        UcliOperationDescriptor operationDescriptor,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (!TryValidateDescriptorDigest(resultIndex, operationDescriptor, result, out errorMessage)
            || !TryValidateResultValue(resultIndex, operationDescriptor, result, out errorMessage))
        {
            return false;
        }

        return TryValidateVerdict(resultIndex, operationDescriptor, result, out errorMessage);
    }

    private static bool TryValidateDescriptorDigest (
        int resultIndex,
        UcliOperationDescriptor operationDescriptor,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (result.OperationDescriptorDigest == null
            || result.OperationDescriptorDigest != operationDescriptor.DescriptorDigest)
        {
            errorMessage = $"Execute response payload is invalid. The 'opResults[{resultIndex}].operationDescriptorDigest' field does not match the prepared descriptor for operation '{operationDescriptor.Name}'.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private bool TryValidateResultValue (
        int resultIndex,
        UcliOperationDescriptor operationDescriptor,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (operationDescriptor.ResultSchemaJson == null)
        {
            return TryValidateResultlessValue(resultIndex, operationDescriptor.Name, result, out errorMessage);
        }

        if (result.Result == null)
        {
            return TryValidateMissingResult(resultIndex, operationDescriptor.Name, result, out errorMessage);
        }

        return RegisteredOperationResultSchemaEvaluator.TryEvaluate(
            operationDescriptor.Name,
            operationDescriptor.ResultSchemaJson,
            result.Result.Value,
            out errorMessage);
    }

    private static bool TryValidateResultlessValue (
        int resultIndex,
        string operationName,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (result.Result != null)
        {
            errorMessage = $"Execute response payload is invalid. Operation '{operationName}' returned 'opResults[{resultIndex}].result' without a result contract.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private bool TryValidateMissingResult (
        int resultIndex,
        string operationName,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        if (IsSuccessfulCall(resultIndex, result))
        {
            errorMessage = $"Execute response payload is invalid. Resultful operation '{operationName}' completed its Call without a result.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private bool TryValidateVerdict (
        int resultIndex,
        UcliOperationDescriptor operationDescriptor,
        OperationExecutionOperationResult result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        var requiresVerdict = IsSuccessfulCall(resultIndex, result)
            && operationDescriptor.VerdictContract != null;
        if (requiresVerdict && result.Verdict == null)
        {
            errorMessage = $"Execute response payload is invalid. Judging operation '{operationDescriptor.Name}' completed its Call without a verdict.";
            return false;
        }

        if (!requiresVerdict && result.Verdict != null)
        {
            errorMessage = $"Execute response payload is invalid. The 'opResults[{resultIndex}].verdict' field is not valid for this operation result.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private bool IsSuccessfulCall (
        int resultIndex,
        OperationExecutionOperationResult result)
    {
        return executedPass == IpcExecuteOperationPhase.Call
            && result.Phase == IpcExecuteOperationPhase.Call
            && ClassifyCompletion(resultIndex, result) == OperationResultCompletion.Completed;
    }

    private OperationResultCompletion ClassifyCompletion (
        int resultIndex,
        OperationExecutionOperationResult result)
    {
        if (HasStepFailure(resultIndex))
        {
            return OperationResultCompletion.Failed;
        }

        if (result.Phase != executedPass
            && HasResponseFailureWithoutInstancePath())
        {
            return OperationResultCompletion.Failed;
        }

        if (result.Phase == IpcExecuteOperationPhase.Skipped
            && HasEarlierStepFailure(resultIndex))
        {
            return OperationResultCompletion.SkippedAfterPriorFailure;
        }

        return OperationResultCompletion.Completed;
    }

    private bool HasEarlierStepFailure (int resultIndex)
    {
        for (var earlierIndex = 0; earlierIndex < resultIndex; earlierIndex++)
        {
            if (HasStepFailure(earlierIndex))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasResponseFailureWithoutInstancePath ()
    {
        for (var i = 0; i < response.Errors.Count; i++)
        {
            if (response.Errors[i].InstancePath == null)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasStepFailure (int resultIndex)
    {
        var stepPath = $"/steps/{resultIndex}";
        var resultPath = $"/opResults/{resultIndex}";
        for (var i = 0; i < response.Errors.Count; i++)
        {
            var instancePath = response.Errors[i].InstancePath;
            if (string.Equals(instancePath, stepPath, StringComparison.Ordinal)
                || instancePath?.StartsWith(stepPath + "/", StringComparison.Ordinal) == true
                || string.Equals(instancePath, resultPath, StringComparison.Ordinal)
                || instancePath?.StartsWith(resultPath + "/", StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }

    private enum OperationResultCompletion
    {
        Completed,
        Failed,
        SkippedAfterPriorFailure,
    }
}
