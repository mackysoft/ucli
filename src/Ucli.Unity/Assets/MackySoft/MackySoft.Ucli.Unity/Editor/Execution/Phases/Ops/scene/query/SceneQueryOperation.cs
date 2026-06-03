using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.SceneInspection;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.query</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneQueryOperation : UcliOperation<SceneQueryArgs, SceneQueryResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<SceneQueryArgs, SceneQueryResult>(
            operationName: UcliPrimitiveOperationNames.SceneQuery,
            kind: UcliOperationKind.Query,
            description: "Finds objects or components in a scene by hierarchy path prefix and component type.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the scene query and observe the selected scene context without applying mutation.",
                callSemantics: "Read selection candidates from the scene hierarchy without applying mutation.",
                touchedContract: "Returns no touched resources because scene query results are observations, not dirty or persisted resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Timeout, cancellation, or source read failure means the candidate set was not fully produced.",
                dangerousNotes: Array.Empty<string>()));

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            SceneQueryArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, out _, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            SceneQueryArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteAsync(operation, args, executionContext, allowTemporaryState: true);
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            SceneQueryArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteAsync(operation, args, executionContext, allowTemporaryState: false);
        }

        private static Task<OperationPhaseStepResult> ExecuteAsync (
            NormalizedOperation operation,
            SceneQueryArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState)
        {
            if (!TryValidate(operation, args, out var scenePath, out var queryArguments, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!SceneQuerySelectionEngine.TryQueryRuntime(
                scenePath,
                queryArguments,
                executionContext,
                allowTemporaryState: allowTemporaryState,
                out var matches,
                out var diagnostics,
                out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities
                    .CreateInvalidArgumentFailure(operation.Id, errorMessage)
                    .WithDiagnostics(diagnostics));
            }

            var payload = new SceneQueryResult(
                scene: scenePath,
                matches: CreatePayloadMatches(matches));
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(payload)).WithDiagnostics(diagnostics));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            SceneQueryArgs args,
            out string scenePath,
            out SceneQuerySelectionEngine.QueryArguments queryArguments,
            out OperationPhaseStepResult? failure)
        {
            failure = null;
            scenePath = args.Scene;
            queryArguments = new SceneQuerySelectionEngine.QueryArguments(args.PathPrefix, args.ComponentType);

            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (queryArguments.ComponentType != null
                && !ComponentTypeResolver.TryResolveComponentType(queryArguments.ComponentType, out _, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            return true;
        }

        private static IReadOnlyList<SceneQueryMatch> CreatePayloadMatches (
            IReadOnlyList<SceneQuerySelectionEngine.QueryMatch> matches)
        {
            var payloadMatches = new SceneQueryMatch[matches.Count];
            for (var i = 0; i < matches.Count; i++)
            {
                payloadMatches[i] = new SceneQueryMatch(
                    kind: matches[i].TargetKind == SceneQuerySelectionEngine.QueryTargetKind.Component ? "component" : "gameObject",
                    hierarchyPath: matches[i].HierarchyPath,
                    componentType: matches[i].ComponentType);
            }

            return payloadMatches;
        }
    }
}
