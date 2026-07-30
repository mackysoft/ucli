using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
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

        var touched = MapTouchedResources(opResult.Touched);
        var diagnostics = MapDiagnostics(opResult.Diagnostics);
        if (opResult.Verdict.HasValue)
        {
            var operationDescriptorDigest = opResult.OperationDescriptorDigest
                ?? throw new InvalidOperationException(
                    "A judging operation result must identify the descriptor used to establish its verdict.");
            var result = opResult.Result
                ?? throw new InvalidOperationException(
                    "A judging operation result must carry the evidence used to establish its verdict.");
            return OperationExecutionOperationResult.CreateJudgingCallResult(
                opResult.Op,
                opResult.Applied,
                opResult.Changed,
                touched,
                operationDescriptorDigest,
                opResult.Verdict.Value,
                result,
                diagnostics);
        }

        return OperationExecutionOperationResult.CreateWithoutVerdict(
            opResult.Op,
            opResult.Phase,
            opResult.Applied,
            opResult.Changed,
            touched,
            opResult.OperationDescriptorDigest,
            opResult.Result,
            diagnostics);
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
                InstancePath: violation.InstancePath,
                Operation: violation.Operation,
                ExpectedFact: violation.ExpectedFact,
                ObservedResult: violation.ObservedResult,
                ApplicationState: violation.ApplicationState);
        }

        return mappedViolations;
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
        string op,
        bool applied,
        bool changed,
        IReadOnlyList<OperationExecutionTouchedResource> touched,
        Sha256Digest operationDescriptorDigest,
        JsonElement result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(op);
        ArgumentNullException.ThrowIfNull(touched);

        return OperationExecutionOperationResult.CreateWithoutVerdict(
            op,
            IpcExecuteOperationPhase.Plan,
            applied,
            changed,
            touched,
            operationDescriptorDigest,
            result,
            diagnostics: Array.Empty<OperationExecutionDiagnostic>());
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
                AssetGuid: touchedResource.AssetGuid);
        }

        return mappedResources;
    }
}
