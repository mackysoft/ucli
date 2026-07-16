using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.SceneInspection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.save</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneSaveOperation : UcliOperation<ScenePathArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<ScenePathArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.SceneSave,
            kind: UcliOperationKind.Mutation,
            description: "Saves a loaded or previewed Unity scene asset.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.SceneSave },
                touchedKinds: new[] { UcliTouchedResourceKind.Scene },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the scene path and observe whether the loaded or previewed scene has save-relevant changes.",
                callSemantics: "Persist the loaded or previewed scene asset when dirty or request-attributed changes exist.",
                touchedContract: "Reports the scene resource when the operation saves or confirms request-attributed scene changes.",
                readPostconditionContract: "Scene tree, GUID path, and readIndex surfaces covering the saved scene may be stale after a successful call.",
                failureSemantics: "Scene save is not transactional; timeout, cancellation, or Unity failure can leave partial or indeterminate scene file changes.",
                dangerousNotes: new[] { "This operation can persist a scene file and is not transactional across Unity save/import steps." }));

        /// <summary> Executes validate phase for <c>ucli.scene.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            ScenePathArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanValidationState(operation, args, executionContext, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.scene.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            ScenePathArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanValidationState(operation, args, executionContext, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var resource = new OperationResource(UcliTouchedResourceKind.Scene, validationState.ScenePath);
            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(resource);
            var hasDirtyScene = validationState.Scene.isDirty;
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: hasRequestAttributedChange || hasDirtyScene,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(resource),
                }));
        }

        /// <summary> Executes call phase for <c>ucli.scene.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            ScenePathArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, executionContext, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (EditorSceneManager.IsPreviewScene(validationState.Scene))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene is not loaded: {validationState.ScenePath}. Use 'ucli.scene.open' first."));
            }

            var resource = new OperationResource(UcliTouchedResourceKind.Scene, validationState.ScenePath);
            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(resource);
            var hasDirtyScene = validationState.Scene.isDirty;
            if (!hasRequestAttributedChange && !hasDirtyScene)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(resource),
                    }));
            }

            if (!EditorSceneManager.SaveScene(validationState.Scene, validationState.ScenePath))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene could not be saved: {validationState.ScenePath}."));
            }

            if (hasRequestAttributedChange)
            {
                executionContext.UnmarkRequestAttributedChange(resource);
            }

            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(resource),
                    })
                .WithPersistence()
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateSceneTreeLite(validationState.ScenePath)));
        }

        /// <summary> Validates operation arguments and resolves loaded scene. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="validationState"> The validated operation state when successful. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            ScenePathArgs args,
            OperationExecutionContext executionContext,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            var scenePath = args.Path.Value;
            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            if (!SceneAssetSourceUtilities.TryGetLoadedScene(scenePath, out var scene, out sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            validationState = new ValidationState(scenePath, scene);
            return true;
        }

        private static bool TryResolvePlanValidationState (
            NormalizedOperation operation,
            ScenePathArgs args,
            OperationExecutionContext executionContext,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            var scenePath = args.Path.Value;
            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            var hasLoadedScene = SceneAssetSourceUtilities.TryGetLoadedScene(scenePath, out var loadedScene, out _);
            if (!hasLoadedScene
                && !executionContext.HasPlannedLiveSceneOpen(scenePath))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene is not loaded: {scenePath}. Use 'ucli.scene.open' first.");
                return false;
            }

            if (executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene))
            {
                validationState = new ValidationState(scenePath, temporaryScene);
                return true;
            }

            if (hasLoadedScene)
            {
                validationState = new ValidationState(scenePath, loadedScene);
                return true;
            }

            failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                operation.Id,
                $"Scene plan state is not available: {scenePath}.");
            return false;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                string scenePath,
                Scene scene)
            {
                ScenePath = scenePath;
                Scene = scene;
            }

            public string ScenePath { get; }

            public Scene Scene { get; }
        }
    }
}
