using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Converts execute responses into normalized application models. </summary>
internal static class ExecuteResponseConverter
{
    /// <summary> Converts one execute response into normalized operation results and errors. </summary>
    /// <param name="response"> The host-decoded Unity response. </param>
    /// <param name="expectedProject"> The locally resolved Unity project targeted by the request. </param>
    /// <returns> The converted execute response. </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="response" /> or <paramref name="expectedProject" /> is <see langword="null" />.
    /// </exception>
    public static ExecuteResponseConversionResult Convert (
        UnityRequestResponse response,
        ResolvedUnityProjectContext expectedProject)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(expectedProject);

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcExecuteResponse payload, out var payloadError))
        {
            return CreateFailure(
                [
                    new OperationExecutionError(
                        UcliCoreErrorCodes.InternalError,
                        $"Execute response payload is invalid. {payloadError.Message}",
                        null),
                ]);
        }

        if (!ProjectIdentityInfo.TryFromHost(
                expectedProject,
                payload.Project,
                out var project,
                out var projectMismatchKind))
        {
            return CreateInvalidPayloadFailure(
                $"Execute response payload is invalid. The 'project.{TextVocabulary.GetText(projectMismatchKind)}' field does not match the requested Unity project.");
        }

        if (!TryValidateContractViolationErrorPair(payload, response.Errors, out var contractViolationPairError))
        {
            return CreateInvalidPayloadFailure(contractViolationPairError);
        }

        return new ExecuteResponseConversionResult(
            OpResults: OperationExecutionModelMapper.MapOpResults(payload.OpResults),
            Errors: response.Errors,
            ContractViolations: OperationExecutionModelMapper.MapContractViolations(payload.ContractViolations),
            PlanToken: payload.PlanToken,
            ReadPostcondition: payload.ReadPostcondition,
            PostReadSource: OperationExecutionModelMapper.MapPostReadSource(payload.PostReadSource),
            Project: project);
    }

    private static bool TryValidateContractViolationErrorPair (
        IpcExecuteResponse payload,
        IReadOnlyList<OperationExecutionError> errors,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var violationOpIds = new HashSet<IpcExecuteStepId>();
        if (payload.ContractViolations != null)
        {
            for (var violationIndex = 0; violationIndex < payload.ContractViolations.Count; violationIndex++)
            {
                violationOpIds.Add(payload.ContractViolations[violationIndex].OpId);
            }
        }

        var errorOpIds = new HashSet<IpcExecuteStepId>();
        for (var errorIndex = 0; errorIndex < errors.Count; errorIndex++)
        {
            var error = errors[errorIndex];
            if (error.Code != ExecuteRequestErrorCodes.OperationContractViolation)
            {
                continue;
            }

            if (error.OpId == null)
            {
                errorMessage = $"Execute response envelope is invalid. The 'errors[{errorIndex}].opId' field must match a contract violation operation.";
                return false;
            }

            errorOpIds.Add(error.OpId);
        }

        if (violationOpIds.Count == 0 && errorOpIds.Count == 0)
        {
            return true;
        }

        if (violationOpIds.Count == 0)
        {
            errorMessage = "Execute response payload is invalid. The 'contractViolations' field must contain at least one item when OPERATION_CONTRACT_VIOLATION is reported.";
            return false;
        }

        if (errorOpIds.Count == 0)
        {
            errorMessage = "Execute response envelope is invalid. OPERATION_CONTRACT_VIOLATION must be reported when 'contractViolations' contains items.";
            return false;
        }

        foreach (var violationOpId in violationOpIds)
        {
            if (!errorOpIds.Contains(violationOpId))
            {
                errorMessage = $"Execute response envelope is invalid. OPERATION_CONTRACT_VIOLATION is missing for contract violation opId '{violationOpId}'.";
                return false;
            }
        }

        foreach (var errorOpId in errorOpIds)
        {
            if (!violationOpIds.Contains(errorOpId))
            {
                errorMessage = $"Execute response payload is invalid. The 'contractViolations' field is missing an item for OPERATION_CONTRACT_VIOLATION opId '{errorOpId}'.";
                return false;
            }
        }

        return true;
    }

    private static ExecuteResponseConversionResult CreateInvalidPayloadFailure (string message)
    {
        return CreateFailure(
            [
                new OperationExecutionError(
                    UcliCoreErrorCodes.InternalError,
                    message,
                    null),
            ]);
    }

    private static ExecuteResponseConversionResult CreateFailure (IReadOnlyList<OperationExecutionError> errors)
    {
        return new ExecuteResponseConversionResult(
            OpResults: [],
            Errors: errors,
            ContractViolations: [],
            PlanToken: null,
            ReadPostcondition: null,
            PostReadSource: null,
            Project: null);
    }
}
