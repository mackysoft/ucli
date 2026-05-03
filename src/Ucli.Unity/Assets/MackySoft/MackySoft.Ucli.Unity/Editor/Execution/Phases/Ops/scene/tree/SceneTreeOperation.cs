using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.tree</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneTreeOperation : TypedUcliOperation<SceneTreeArgs, SceneTreeResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<SceneTreeArgs, SceneTreeResult>(
            operationName: UcliPrimitiveOperationNames.SceneTree,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            describeContract: UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.SceneTree));

        /// <summary> Executes validate phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, executionContext, allowTemporaryState: true, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Execute(operation, args, executionContext, applied: false);
        }

        /// <summary> Executes call phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Execute(operation, args, executionContext, applied: true);
        }

        /// <summary> Executes shared plan/call flow. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="applied"> The applied flag for success. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            bool applied)
        {
            if (!TryValidateArguments(operation, args, executionContext, allowTemporaryState: !applied, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var tree = new SceneTreeResult(
                validationState.ScenePath,
                SceneTreeNodeSnapshotBuilder.BuildRoots(validationState.Scene, validationState.Depth, executionContext));
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
                },
                result: IpcPayloadCodec.SerializeToElement(tree)));
        }

        /// <summary> Validates operation arguments and resolves loaded scene. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="validationState"> The validated operation state when successful. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            if (args.Depth < 0)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Operation 'args.depth' must be greater than or equal to 0.");
                return false;
            }

            var scenePath = args.Path;
            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(scenePath, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveScene(scenePath, executionContext, allowTemporaryState, out var scene, out sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            validationState = new ValidationState(scenePath, scene, args.Depth);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                string scenePath,
                Scene scene,
                int? depth)
            {
                ScenePath = scenePath;
                Scene = scene;
                Depth = depth;
            }

            public string ScenePath { get; }

            public Scene Scene { get; }

            public int? Depth { get; }
        }
    }
}
