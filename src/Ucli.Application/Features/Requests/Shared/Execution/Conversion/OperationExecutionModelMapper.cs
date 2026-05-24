using System.Text.Json;
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
            Diagnostics = MapDiagnostics(opResult.Diagnostics),
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

    /// <summary> Maps runtime operation-result contract violations. </summary>
    public static IReadOnlyList<OperationExecutionContractViolation> MapContractViolations (
        IReadOnlyList<IpcExecuteContractViolation>? contractViolations)
    {
        if (contractViolations == null || contractViolations.Count == 0)
        {
            return [];
        }

        var mappedViolations = new OperationExecutionContractViolation[contractViolations.Count];
        for (var i = 0; i < contractViolations.Count; i++)
        {
            var violation = contractViolations[i];
            mappedViolations[i] = new OperationExecutionContractViolation(
                OpId: violation.OpId,
                Operation: violation.Operation,
                ExpectedFact: violation.ExpectedFact,
                ObservedResult: violation.ObservedResult,
                ApplicationState: violation.ApplicationState);
        }

        return mappedViolations;
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

    /// <summary> Maps one optional post-read source contract. </summary>
    public static OperationExecutionPostReadSource? MapPostReadSource (IpcExecutePostReadSource? postReadSource)
    {
        if (postReadSource == null)
        {
            return null;
        }

        var steps = postReadSource.Steps;
        var mappedSteps = new OperationExecutionPostReadSourceStep[steps.Count];
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            mappedSteps[i] = new OperationExecutionPostReadSourceStep(
                OpId: step.OpId,
                SourceKind: step.SourceKind,
                PlayModeMutation: step.PlayModeMutation,
                Commit: step.Commit,
                PersistenceExpected: step.PersistenceExpected,
                ExpectedPostState: step.ExpectedPostState);
        }

        return new OperationExecutionPostReadSource(postReadSource.SchemaVersion, mappedSteps);
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

    private static IReadOnlyList<OperationExecutionDiagnostic> MapDiagnostics (IReadOnlyList<IpcExecuteDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var mappedDiagnostics = new OperationExecutionDiagnostic[diagnostics.Count];
        for (var i = 0; i < diagnostics.Count; i++)
        {
            var diagnostic = diagnostics[i];
            mappedDiagnostics[i] = new OperationExecutionDiagnostic(
                Code: diagnostic.Code,
                Severity: diagnostic.Severity,
                CoverageImpact: diagnostic.CoverageImpact,
                Message: diagnostic.Message);
        }

        return mappedDiagnostics;
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
