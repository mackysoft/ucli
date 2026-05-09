using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.open</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneOpenOperation : UcliOperation<ScenePathArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<ScenePathArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.SceneOpen,
            kind: UcliOperationKind.Command,
            policy: OperationPolicy.Safe,
            description: "Opens a Unity scene asset in the editor.",
            assurance: new UcliOperationAssuranceContract(
                new[] { UcliOperationSideEffect.OpensSceneInEditor },
                mayDirty: false,
                mayPersist: false,
                new[] { IpcExecuteTouchedResourceKindNames.Scene },
                UcliOperationPlanMode.MayCreatePreviewState));

        /// <summary> Executes validate phase for <c>ucli.scene.open</c>. </summary>
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
            if (!TryValidateArguments(operation, args, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.scene.open</c>. </summary>
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
            if (!TryValidateArguments(operation, args, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(validationState.ScenePath, out _, out _)
                && !SceneOperationUtilities.TryEnsureCanOpenSceneLive(validationState.ScenePath, out var blockerErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    blockerErrorMessage));
            }

            if (!executionContext.TryGetOrOpenTemporaryScene(validationState.ScenePath, out _, out var sceneErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    sceneErrorMessage));
            }

            executionContext.TrackPlannedLiveSceneOpen(validationState.ScenePath);

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
                }));
        }

        /// <summary> Executes call phase for <c>ucli.scene.open</c>. </summary>
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
            if (!TryValidateArguments(operation, args, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (SceneOperationUtilities.TryGetLoadedScene(validationState.ScenePath, out _, out _))
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: true,
                    changed: false,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
                    }));
            }

            if (!SceneOperationUtilities.TryEnsureCanOpenSceneLive(validationState.ScenePath, out var blockerErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    blockerErrorMessage));
            }

            var openedScene = EditorSceneManager.OpenScene(validationState.ScenePath, OpenSceneMode.Single);
            if (!openedScene.IsValid() || !openedScene.isLoaded)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene could not be opened: {validationState.ScenePath}."));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
                }));
        }

        /// <summary> Validates operation arguments and scene path. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="validationState"> The validated operation state when successful. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            ScenePathArgs args,
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

            validationState = new ValidationState(args.Path);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (string scenePath)
            {
                ScenePath = scenePath;
            }

            public string ScenePath { get; }
        }
    }
}
