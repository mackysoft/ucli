using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;
using MackySoft.Ucli.Contracts.Operations;

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
            description: "Returns a GameObject description including components and child hierarchy.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the GameObject selector and observe the selected scene or prefab context without applying mutation.",
                callSemantics: "Read the selected GameObject structure and component data without applying mutation.",
                touchedContract: "Returns no touched resources because GameObject description data is observational, not dirty or persisted state.",
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
            return ExecuteAsync(operation, args, executionContext, allowTemporaryState: true);
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
            return ExecuteAsync(operation, args, executionContext, allowTemporaryState: false);
        }

        /// <summary> Executes the shared plan/call flow. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan state may satisfy target resolution. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> ExecuteAsync (
            NormalizedOperation operation,
            GoDescribeArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState)
        {
            if (!TryValidateArguments(
                operation,
                args,
                executionContext,
                allowTemporaryState,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var description = allowTemporaryState
                ? GameObjectDescriptionBuilder.Build(
                    validationState.Target,
                    validationState.Depth,
                    executionContext,
                    includeTemporaryState: true)
                : GameObjectDescriptionBuilder.Build(validationState.Target, validationState.Depth);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
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
            if (!UnityObjectReferenceContractMapper.TryMap(
                    args.Target,
                    "args.target",
                    operation.AliasReferences,
                    out var targetReference,
                    out var targetErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, targetErrorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                targetReference,
                executionContext,
                OperationObjectReferenceUtilities.GetReferenceResolutionPolicy(operation, allowTemporaryState),
                out var targetResolution,
                out targetErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, targetErrorMessage);
                return false;
            }

            var target = targetResolution.GameObject;
            if (target == null)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Reference did not resolve to a GameObject.");
                return false;
            }

            validationState = new ValidationState(
                target,
                args.Depth);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                int? depth)
            {
                Target = target;
                Depth = depth;
            }

            public GameObject Target { get; }

            public int? Depth { get; }
        }
    }
}
