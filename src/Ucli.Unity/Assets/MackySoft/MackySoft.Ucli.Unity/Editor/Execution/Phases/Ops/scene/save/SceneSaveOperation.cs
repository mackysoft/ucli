using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.save</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneSaveOperation : TypedUcliOperation<PathArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<PathArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.SceneSave,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            describeContract: UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.SceneSave));

        /// <summary> Executes validate phase for <c>ucli.scene.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            PathArgs args,
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
        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            PathArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanValidationState(operation, args, executionContext, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var resource = new OperationResource(OperationTouchKind.Scene, validationState.ScenePath);
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
        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            PathArgs args,
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

            var resource = new OperationResource(OperationTouchKind.Scene, validationState.ScenePath);
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
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateSceneTreeLite(validationState.ScenePath)));
        }

        /// <summary> Validates operation arguments and resolves loaded scene. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="validationState"> The validated operation state when successful. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            PathArgs args,
            OperationExecutionContext executionContext,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(args.Path, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(args.Path, out var scene, out sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            validationState = new ValidationState(args.Path, scene);
            return true;
        }

        private static bool TryResolvePlanValidationState (
            NormalizedOperation operation,
            PathArgs args,
            OperationExecutionContext executionContext,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(args.Path, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            var hasLoadedScene = SceneOperationUtilities.TryGetLoadedScene(args.Path, out var loadedScene, out _);
            if (!hasLoadedScene
                && !executionContext.HasPlannedLiveSceneOpen(args.Path))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene is not loaded: {args.Path}. Use 'ucli.scene.open' first.");
                return false;
            }

            if (executionContext.TryGetTemporaryScene(args.Path, out var temporaryScene))
            {
                validationState = new ValidationState(args.Path, temporaryScene);
                return true;
            }

            if (hasLoadedScene)
            {
                validationState = new ValidationState(args.Path, loadedScene);
                return true;
            }

            failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                operation.Id,
                $"Scene plan state is not available: {args.Path}.");
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
