using System;
using System.Collections.Generic;
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
    /// <summary> Implements <c>ucli.go.reparent</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoReparentOperation : UcliOperation<GoReparentArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GoReparentArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.GoReparent,
            kind: UcliOperationKind.Mutation,
            description: "Moves a GameObject under a new parent GameObject.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.SceneContentMutation, UcliOperationSideEffect.PrefabContentMutation },
                touchedKinds: new[] { UcliTouchedResourceKindNames.Scene, UcliTouchedResourceKindNames.Prefab },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate source and parent targets, then report the expected hierarchy impact without creating preview state or mutating live Unity state.",
                callSemantics: "Reparent the GameObject in live Unity state and leave saving to explicit save operations.",
                touchedContract: "Reports the scene or prefab resource dirtied by the hierarchy mutation when the target can be resolved.",
                readPostconditionContract: "Scene, prefab, and hierarchy read surfaces covering touched resources may be stale until refreshed.",
                failureSemantics: "Failure before apply leaves no requested mutation; failure during apply may leave live Unity state partially changed.",
                dangerousNotes: new[] { "This operation can dirty scene or prefab hierarchy state without persisting it; callers must save or discard changes explicitly." }));

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            GoReparentArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, executionContext, allowTemporaryState: true, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            GoReparentArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allowTemporaryState = operation.SourceKind == NormalizedOperation.SourceStepKind.Edit;
            if (!TryValidate(operation, args, executionContext, allowTemporaryState, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (operation.SourceKind == NormalizedOperation.SourceStepKind.Op)
            {
                if (state.Target.transform.parent == state.Parent.transform)
                {
                    return Task.FromResult(OperationPhaseStepResult.Success(
                        applied: false,
                        changed: false));
                }

                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: true,
                    touched: CreateTouched(state)));
            }

            if (!GoOperationUtilities.TryEnsurePlanResourceState(
                    state.TargetResource,
                    executionContext,
                    out var targetPreparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    targetPreparationErrorMessage));
            }

            if (!AreSameResource(state.TargetResource, state.ParentResource)
                && !GoOperationUtilities.TryEnsurePlanResourceState(
                    state.ParentResource,
                    executionContext,
                    out var parentPreparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    parentPreparationErrorMessage));
            }

            if (!TryValidate(operation, args, executionContext, allowTemporaryState: true, out state, out failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                state.Target,
                state.TargetResource,
                executionContext,
                out var targetErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, targetErrorMessage));
            }

            if (!GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                state.Parent,
                state.ParentResource,
                executionContext,
                out var parentErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parentErrorMessage));
            }

            if (state.Target.transform.parent == state.Parent.transform)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false));
            }

            state.Target.transform.SetParent(state.Parent.transform, worldPositionStays: false);
            GoOperationUtilities.MarkPlanResourceDirty(state.TargetResource, executionContext);
            executionContext.MarkRequestAttributedChange(state.TargetResource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: CreateTouched(state)));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            GoReparentArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, executionContext, allowTemporaryState: false, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (state.Target.transform.parent == state.Parent.transform)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: true,
                    changed: false));
            }

            state.Target.transform.SetParent(state.Parent.transform, worldPositionStays: false);
            executionContext.MarkRequestAttributedChange(state.TargetResource);
            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: CreateTouched(state))
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateSceneTreeLiteForSceneResource(state.TargetResource)));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            GoReparentArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState state,
            out OperationPhaseStepResult? failure)
        {
            state = default;
            failure = null;
            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var targetReference, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!UnityObjectReferenceContractMapper.TryMap(args.Parent, "args.parent", out var parentReference, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                targetReference,
                executionContext,
                OperationObjectReferenceUtilities.GetReferenceResolutionPolicy(operation, allowTemporaryState),
                out var targetResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                parentReference,
                executionContext,
                OperationObjectReferenceUtilities.GetReferenceResolutionPolicy(operation, allowTemporaryState),
                out var parentResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AreSameResource(targetResolution.Resource, parentResolution.Resource))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, "Reparent target and parent must belong to the same editable context.");
                return false;
            }

            if (targetResolution.GameObject == parentResolution.GameObject
                || parentResolution.GameObject!.transform.IsChildOf(targetResolution.GameObject!.transform))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, "Reparent would create a transform cycle.");
                return false;
            }

            state = new ValidationState(
                targetResolution.GameObject!,
                parentResolution.GameObject!,
                targetResolution.Resource,
                parentResolution.Resource);
            return true;
        }

        private static bool AreSameResource (
            OperationResource left,
            OperationResource right)
        {
            return left.Kind == right.Kind && string.Equals(left.Path, right.Path, StringComparison.Ordinal);
        }

        private static OperationTouch[] CreateTouched (in ValidationState state)
        {
            if (AreSameResource(state.TargetResource, state.ParentResource))
            {
                return new[]
                {
                    OperationResourceUtilities.CreateTouch(state.TargetResource),
                };
            }

            return new[]
            {
                OperationResourceUtilities.CreateTouch(state.TargetResource),
                OperationResourceUtilities.CreateTouch(state.ParentResource),
            };
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                GameObject parent,
                OperationResource targetResource,
                OperationResource parentResource)
            {
                Target = target;
                Parent = parent;
                TargetResource = targetResource;
                ParentResource = parentResource;
            }

            public GameObject Target { get; }

            public GameObject Parent { get; }

            public OperationResource TargetResource { get; }

            public OperationResource ParentResource { get; }
        }
    }
}
