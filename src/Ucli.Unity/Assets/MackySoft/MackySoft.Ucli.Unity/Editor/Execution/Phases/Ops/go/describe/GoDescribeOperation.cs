using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.go.describe</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoDescribeOperation : UcliOperation<GoDescribeArgs, GameObjectDescriptionResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            operationName: UcliPrimitiveOperationNames.GoDescribe,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            description: "Returns a GameObject description including components and child hierarchy.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                touchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Scene, IpcExecuteTouchedResourceKindNames.Prefab },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the GameObject selector and observe the selected scene or prefab context without applying mutation.",
                callSemantics: "Read the selected GameObject structure and component data without applying mutation.",
                touchedContract: "Reports the scene or prefab resource that contains the observed GameObject.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Timeout, cancellation, or unresolved selector failure means the GameObject description was not fully produced.",
                dangerousNotes: Array.Empty<string>()));

        /// <summary> Executes validate phase for <c>ucli.go.describe</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            GoDescribeArgs args,
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

        /// <summary> Executes plan phase for <c>ucli.go.describe</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            GoDescribeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteAsync(operation, args, executionContext, applied: false);
        }

        /// <summary> Executes call phase for <c>ucli.go.describe</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            GoDescribeArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteAsync(operation, args, executionContext, applied: true);
        }

        /// <summary> Executes the shared plan/call flow. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="applied"> The applied flag for the successful phase result. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> ExecuteAsync (
            NormalizedOperation operation,
            GoDescribeArgs args,
            OperationExecutionContext executionContext,
            bool applied)
        {
            if (!TryValidateArguments(
                operation,
                args,
                executionContext,
                allowTemporaryState: !applied,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var description = applied
                ? GameObjectDescriptionBuilder.Build(validationState.Target, validationState.Depth)
                : GameObjectDescriptionBuilder.Build(
                    validationState.Target,
                    validationState.Depth,
                    executionContext,
                    includeTemporaryState: true);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(validationState.Resource),
                },
                result: IpcPayloadCodec.SerializeToElement(description)));
        }

        /// <summary> Validates arguments and resolves the target editable GameObject. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan aliases may satisfy target resolution. </param>
        /// <param name="validationState"> The validated operation state when validation succeeds. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            GoDescribeArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var targetReference, out var targetErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, targetErrorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                targetReference,
                executionContext,
                allowTemporaryState,
                out var targetResolution,
                out targetErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, targetErrorMessage);
                return false;
            }

            validationState = new ValidationState(
                targetResolution.GameObject!,
                targetResolution.Resource,
                args.Depth);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                OperationResource resource,
                int? depth)
            {
                Target = target;
                Resource = resource;
                Depth = depth;
            }

            public GameObject? Target { get; }

            public OperationResource Resource { get; }

            public int? Depth { get; }
        }
    }
}
