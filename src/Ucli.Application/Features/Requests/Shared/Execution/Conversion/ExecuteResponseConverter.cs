using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Converts execute IPC responses into normalized CLI-facing models. </summary>
internal static class ExecuteResponseConverter
{
    /// <summary> Converts one execute IPC response into normalized operation results and errors. </summary>
    /// <param name="response"> The Unity IPC response. </param>
    /// <returns> The converted execute response. </returns>
    public static ExecuteResponseConversionResult Convert (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcExecuteResponse? payload, out var payloadError))
        {
            return CreateFailure(
                [
                    new IpcError(
                        IpcErrorCodes.InternalError,
                        $"Execute response payload is invalid. {payloadError.Message}",
                        null),
                ]);
        }

        if (!TryValidatePayload(payload, out var payloadValidationError))
        {
            return CreateInvalidPayloadFailure(payloadValidationError);
        }

        if (!TryValidateErrors(response.Errors, out var errorsValidationError))
        {
            return CreateInvalidPayloadFailure(errorsValidationError);
        }

        var normalizedErrors = NormalizeErrors(response.Status, response.Errors);
        var validatedPayload = payload!;
        return new ExecuteResponseConversionResult(
            OpResults: validatedPayload.OpResults,
            Errors: normalizedErrors,
            Outcome: ResolveOutcome(normalizedErrors),
            PlanToken: validatedPayload.PlanToken,
            ReadPostcondition: validatedPayload.ReadPostcondition);
    }

    /// <summary> Resolves the application outcome from one machine-readable error code. </summary>
    /// <param name="errorCode"> The raw error code. </param>
    /// <returns> The associated application outcome. </returns>
    public static ApplicationOutcome ResolveOutcome (string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return string.Equals(errorCode, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }

    /// <summary> Resolves the application outcome from one machine-readable error collection. </summary>
    /// <param name="errors"> The machine-readable error collection. </param>
    /// <returns> The associated application outcome. </returns>
    public static ApplicationOutcome ResolveOutcome (IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            return ApplicationOutcome.Success;
        }

        return errors.All(static error => string.Equals(error.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }

    private static IReadOnlyList<IpcError> NormalizeErrors (
        string? status,
        IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (string.Equals(status, IpcProtocol.StatusOk, StringComparison.Ordinal)
            && errors.Count == 0)
        {
            return [];
        }

        if (errors.Count != 0)
        {
            return errors;
        }

        return
        [
            new IpcError(
                IpcErrorCodes.InternalError,
                $"Execute response failed with status '{status}'.",
                null),
        ];
    }

    private static bool TryValidatePayload (
        IpcExecuteResponse? payload,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (payload == null || payload.OpResults is null)
        {
            errorMessage = "Execute response payload is invalid. The 'opResults' field is missing.";
            return false;
        }

        for (var opResultIndex = 0; opResultIndex < payload.OpResults.Count; opResultIndex++)
        {
            var opResult = payload.OpResults[opResultIndex];
            if (opResult == null)
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}]' item is missing.";
                return false;
            }

            if (IsMissingRequiredString(opResult.OpId))
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].opId' field is missing.";
                return false;
            }

            if (IsMissingRequiredString(opResult.Op))
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].op' field is missing.";
                return false;
            }

            if (IsMissingRequiredString(opResult.Phase))
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].phase' field is missing.";
                return false;
            }

            if (opResult.Touched is null)
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].touched' field is missing.";
                return false;
            }

            for (var touchedIndex = 0; touchedIndex < opResult.Touched.Count; touchedIndex++)
            {
                if (opResult.Touched[touchedIndex] == null)
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].touched[{touchedIndex}]' item is missing.";
                    return false;
                }

                if (IsMissingRequiredString(opResult.Touched[touchedIndex].Kind))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].touched[{touchedIndex}].kind' field is missing.";
                    return false;
                }

                if (IsMissingRequiredString(opResult.Touched[touchedIndex].Path))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].touched[{touchedIndex}].path' field is missing.";
                    return false;
                }
            }
        }

        if (payload.ReadPostcondition == null)
        {
            return true;
        }

        if (payload.ReadPostcondition.Requirements is null)
        {
            errorMessage = "Execute response payload is invalid. The 'readPostcondition.requirements' field is missing.";
            return false;
        }

        for (var requirementIndex = 0; requirementIndex < payload.ReadPostcondition.Requirements.Count; requirementIndex++)
        {
            if (payload.ReadPostcondition.Requirements[requirementIndex] == null)
            {
                errorMessage = $"Execute response payload is invalid. The 'readPostcondition.requirements[{requirementIndex}]' item is missing.";
                return false;
            }

            if (IsMissingRequiredString(payload.ReadPostcondition.Requirements[requirementIndex].Surface))
            {
                errorMessage = $"Execute response payload is invalid. The 'readPostcondition.requirements[{requirementIndex}].surface' field is missing.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateErrors (
        IReadOnlyList<IpcError>? errors,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (errors is null)
        {
            errorMessage = "Execute response envelope is invalid. The 'errors' field is missing.";
            return false;
        }

        for (var errorIndex = 0; errorIndex < errors.Count; errorIndex++)
        {
            if (errors[errorIndex] == null)
            {
                errorMessage = $"Execute response envelope is invalid. The 'errors[{errorIndex}]' item is missing.";
                return false;
            }

            if (IsMissingRequiredString(errors[errorIndex].Code))
            {
                errorMessage = $"Execute response envelope is invalid. The 'errors[{errorIndex}].code' field is missing.";
                return false;
            }

            if (IsMissingRequiredString(errors[errorIndex].Message))
            {
                errorMessage = $"Execute response envelope is invalid. The 'errors[{errorIndex}].message' field is missing.";
                return false;
            }
        }

        return true;
    }

    private static bool IsMissingRequiredString (string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    private static ExecuteResponseConversionResult CreateInvalidPayloadFailure (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return CreateFailure(
            [
                new IpcError(
                    IpcErrorCodes.InternalError,
                    message,
                    null),
            ]);
    }

    private static ExecuteResponseConversionResult CreateFailure (IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return new ExecuteResponseConversionResult(
            OpResults: [],
            Errors: errors,
            Outcome: ResolveOutcome(errors),
            PlanToken: null,
            ReadPostcondition: null);
    }
}
