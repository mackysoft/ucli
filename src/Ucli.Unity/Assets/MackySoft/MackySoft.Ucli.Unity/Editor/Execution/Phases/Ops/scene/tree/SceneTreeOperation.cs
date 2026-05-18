using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.tree</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneTreeOperation : UcliOperation<SceneTreeArgs, SceneTreeResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<SceneTreeArgs, SceneTreeResult>(
            operationName: UcliPrimitiveOperationNames.SceneTree,
            kind: UcliOperationKind.Query,
            description: "Returns the hierarchy tree for a Unity scene.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the scene path and observe the selected hierarchy source without applying mutation.",
                callSemantics: "Read the scene hierarchy without applying mutation.",
                touchedContract: "Returns no touched resources because scene hierarchy data is observational, not dirty or persisted state.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Timeout, cancellation, or source fallback failure means the hierarchy was not fully observed.",
                dangerousNotes: Array.Empty<string>()));

        /// <summary> Executes validate phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, executionContext, allowTemporaryState: true, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            validationState.SceneLease.Dispose();
            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecutePhaseAsync(operation, args, executionContext, applied: false);
        }

        /// <summary> Executes call phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecutePhaseAsync(operation, args, executionContext, applied: false, allowTemporaryState: false);
        }

        /// <summary> Executes shared plan/call flow. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="applied"> The applied flag for success. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> ExecutePhaseAsync (
            NormalizedOperation operation,
            SceneTreeArgs args,
            OperationExecutionContext executionContext,
            bool applied,
            bool allowTemporaryState = true)
        {
            if (!TryValidateArguments(operation, args, executionContext, allowTemporaryState, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            using (validationState.SceneLease)
            {
                var roots = SceneTreeNodeSnapshotBuilder.BuildRoots(validationState.SceneLease.Scene, validationState.Depth, executionContext);
                var windowedRoots = SceneTreeWindowProjector.Apply(roots, validationState.WindowOptions);
                var tree = new SceneTreeResult(
                    validationState.ScenePath,
                    windowedRoots.Items,
                    validationState.SceneLease.CreateSourceState(),
                    windowedRoots.Window);
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: applied,
                    changed: false,
                    result: IpcPayloadCodec.SerializeToElement(tree)));
            }
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

            var windowOptions = BoundedWindowOptionsNormalizer.NormalizeValidated(args.Limit, args.Cursor);

            var scenePath = args.Path;
            var policy = allowTemporaryState
                ? SceneSourceResolver.Policy.TrackedTemporaryOrLoadedOrPersistedPreview
                : SceneSourceResolver.Policy.LoadedOrPersistedPreview;
            string sceneErrorMessage;
            if (!SceneSourceResolver.TryAcquire(scenePath, policy, executionContext, out var sceneLease, out sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            validationState = new ValidationState(scenePath, sceneLease, args.Depth, windowOptions);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                string scenePath,
                SceneSourceResolver.Lease sceneLease,
                int? depth,
                BoundedWindowOptions windowOptions)
            {
                ScenePath = scenePath;
                SceneLease = sceneLease;
                Depth = depth;
                WindowOptions = windowOptions;
            }

            public string ScenePath { get; }

            public SceneSourceResolver.Lease SceneLease { get; }

            public int? Depth { get; }

            public BoundedWindowOptions WindowOptions { get; }
        }
    }
}
