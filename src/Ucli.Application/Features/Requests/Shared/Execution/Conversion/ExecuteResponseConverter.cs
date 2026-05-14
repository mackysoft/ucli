using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;

/// <summary> Converts execute responses into normalized application models. </summary>
internal static class ExecuteResponseConverter
{
    /// <summary> Converts one execute response into normalized operation results and errors. </summary>
    /// <param name="response"> The host-decoded Unity response. </param>
    /// <returns> The converted execute response. </returns>
    public static ExecuteResponseConversionResult Convert (UnityRequestResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcExecuteResponse? payload, out var payloadError))
        {
            return CreateFailure(
                [
                    new OperationExecutionError(
                        UcliCoreErrorCodes.InternalError,
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

        var normalizedErrors = NormalizeErrors(response.HasFailureStatus, response.FailureStatus, response.Errors);
        var validatedPayload = payload!;
        return new ExecuteResponseConversionResult(
            OpResults: OperationExecutionModelMapper.MapOpResults(validatedPayload.OpResults),
            Errors: normalizedErrors,
            PlanToken: validatedPayload.PlanToken,
            ReadPostcondition: OperationExecutionModelMapper.MapReadPostcondition(validatedPayload.ReadPostcondition));
    }

    private static IReadOnlyList<OperationExecutionError> NormalizeErrors (
        bool hasFailureStatus,
        string? failureStatus,
        IReadOnlyList<OperationExecutionError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (!hasFailureStatus && errors.Count == 0)
        {
            return [];
        }

        if (errors.Count != 0)
        {
            return errors;
        }

        return
        [
            new OperationExecutionError(
                UcliCoreErrorCodes.InternalError,
                string.IsNullOrWhiteSpace(failureStatus)
                    ? "Execute response failed with an error status."
                    : $"Execute response failed with status '{failureStatus}'.",
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

        if (payload.Project == null)
        {
            errorMessage = "Execute response payload is invalid. The 'project' field is missing.";
            return false;
        }

        if (IsMissingRequiredString(payload.Project.ProjectPath))
        {
            errorMessage = "Execute response payload is invalid. The 'project.projectPath' field is missing.";
            return false;
        }

        if (IsMissingRequiredString(payload.Project.ProjectFingerprint))
        {
            errorMessage = "Execute response payload is invalid. The 'project.projectFingerprint' field is missing.";
            return false;
        }

        if (IsMissingRequiredString(payload.Project.UnityVersion))
        {
            errorMessage = "Execute response payload is invalid. The 'project.unityVersion' field is missing.";
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

            if (!IsKnownOperationPhase(opResult.Phase))
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].phase' value is unsupported. Actual: {opResult.Phase}";
                return false;
            }

            if (opResult.Touched is null)
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].touched' field is missing.";
                return false;
            }

            if (opResult.Diagnostics is null)
            {
                errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics' field is missing.";
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

                if (!IsKnownTouchedResourceKind(opResult.Touched[touchedIndex].Kind))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].touched[{touchedIndex}].kind' value is unsupported. Actual: {opResult.Touched[touchedIndex].Kind}";
                    return false;
                }

                if (IsMissingRequiredString(opResult.Touched[touchedIndex].Path))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].touched[{touchedIndex}].path' field is missing.";
                    return false;
                }
            }

            for (var diagnosticIndex = 0; diagnosticIndex < opResult.Diagnostics.Count; diagnosticIndex++)
            {
                var diagnostic = opResult.Diagnostics[diagnosticIndex];
                if (diagnostic == null)
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics[{diagnosticIndex}]' item is missing.";
                    return false;
                }

                if (!diagnostic.Code.IsValid)
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics[{diagnosticIndex}].code' field is missing.";
                    return false;
                }

                if (IsMissingRequiredString(diagnostic.Severity))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics[{diagnosticIndex}].severity' field is missing.";
                    return false;
                }

                if (!IsKnownDiagnosticSeverity(diagnostic.Severity))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics[{diagnosticIndex}].severity' value is unsupported. Actual: {diagnostic.Severity}";
                    return false;
                }

                if (IsMissingRequiredString(diagnostic.CoverageImpact))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics[{diagnosticIndex}].coverageImpact' field is missing.";
                    return false;
                }

                if (!IsKnownDiagnosticCoverageImpact(diagnostic.CoverageImpact))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics[{diagnosticIndex}].coverageImpact' value is unsupported. Actual: {diagnostic.CoverageImpact}";
                    return false;
                }

                if (IsMissingRequiredString(diagnostic.Message))
                {
                    errorMessage = $"Execute response payload is invalid. The 'opResults[{opResultIndex}].diagnostics[{diagnosticIndex}].message' field is missing.";
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

            if (!IsKnownReadPostconditionSurface(payload.ReadPostcondition.Requirements[requirementIndex].Surface))
            {
                errorMessage = $"Execute response payload is invalid. The 'readPostcondition.requirements[{requirementIndex}].surface' value is unsupported. Actual: {payload.ReadPostcondition.Requirements[requirementIndex].Surface}";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateErrors (
        IReadOnlyList<OperationExecutionError>? errors,
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

            if (!errors[errorIndex].Code.IsValid)
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

    private static bool IsKnownOperationPhase (string phase)
    {
        return phase is IpcExecuteOperationPhaseNames.Validate
            or IpcExecuteOperationPhaseNames.Plan
            or IpcExecuteOperationPhaseNames.Call
            or IpcExecuteOperationPhaseNames.Skipped;
    }

    private static bool IsKnownTouchedResourceKind (string kind)
    {
        return kind is IpcExecuteTouchedResourceKindNames.Scene
            or IpcExecuteTouchedResourceKindNames.Prefab
            or IpcExecuteTouchedResourceKindNames.Asset
            or IpcExecuteTouchedResourceKindNames.ProjectSettings;
    }

    private static bool IsKnownReadPostconditionSurface (string surface)
    {
        return surface is IpcExecuteReadPostconditionSurfaceNames.AssetSearch
            or IpcExecuteReadPostconditionSurfaceNames.GuidPath
            or IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite;
    }

    private static bool IsKnownDiagnosticSeverity (string severity)
    {
        return severity is IpcExecuteDiagnosticSeverityNames.Info
            or IpcExecuteDiagnosticSeverityNames.Warning
            or IpcExecuteDiagnosticSeverityNames.Error;
    }

    private static bool IsKnownDiagnosticCoverageImpact (string coverageImpact)
    {
        return coverageImpact is IpcExecuteDiagnosticCoverageImpactNames.None
            or IpcExecuteDiagnosticCoverageImpactNames.Partial
            or IpcExecuteDiagnosticCoverageImpactNames.Indeterminate;
    }

    private static ExecuteResponseConversionResult CreateInvalidPayloadFailure (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

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
        ArgumentNullException.ThrowIfNull(errors);

        return new ExecuteResponseConversionResult(
            OpResults: [],
            Errors: errors,
            PlanToken: null,
            ReadPostcondition: null);
    }
}
