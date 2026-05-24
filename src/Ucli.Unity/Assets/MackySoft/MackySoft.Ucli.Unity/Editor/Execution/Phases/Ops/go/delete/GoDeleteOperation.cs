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
    /// <summary> Implements <c>ucli.go.delete</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoDeleteOperation : UcliOperation<GoTargetArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GoTargetArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.GoDelete,
            kind: UcliOperationKind.Mutation,
            description: "Deletes a GameObject from a scene or prefab hierarchy.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.SceneContentMutation, UcliOperationSideEffect.PrefabContentMutation },
                touchedKinds: new[] { UcliTouchedResourceKindNames.Scene, UcliTouchedResourceKindNames.Prefab },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the GameObject target and report the expected hierarchy impact without creating preview state or mutating live Unity state.",
                callSemantics: "Delete the GameObject from live Unity state and leave saving to explicit save operations.",
                touchedContract: "Reports the scene or prefab resource dirtied by the hierarchy mutation when the target can be resolved.",
                readPostconditionContract: "Scene, prefab, and hierarchy read surfaces covering touched resources may be stale until refreshed.",
                failureSemantics: "Failure before apply leaves no requested mutation; failure during apply may leave live Unity state partially changed.",
                dangerousNotes: new[] { "This operation can dirty scene or prefab hierarchy state without persisting it; callers must save or discard changes explicitly." }));

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            GoTargetArgs args,
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
            GoTargetArgs args,
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
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: true,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(state.Resource),
                    }));
            }

            if (!GoOperationUtilities.TryEnsurePlanResourceState(
                    state.Resource,
                    executionContext,
                    out var preparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    preparationErrorMessage));
            }

            if (!TryValidate(operation, args, executionContext, allowTemporaryState: true, out state, out failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                state.Target,
                state.Resource,
                executionContext,
                out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            RegisterDeletedGlobalObjectIds(state.Target, state.Resource, executionContext);
            Object.DestroyImmediate(state.Target);
            GoOperationUtilities.MarkPlanResourceDirty(state.Resource, executionContext);
            executionContext.MarkRequestAttributedChange(state.Resource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(state.Resource),
                }));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            GoTargetArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, executionContext, allowTemporaryState: false, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            Object.DestroyImmediate(state.Target);
            executionContext.MarkRequestAttributedChange(state.Resource);
            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(state.Resource),
                    })
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateSceneTreeLiteForSceneResource(state.Resource)));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            GoTargetArgs args,
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

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                targetReference,
                executionContext,
                allowTemporaryState,
                out var targetResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!TryValidateDeletionTarget(
                    targetResolution.GameObject!,
                    targetResolution.Resource,
                    executionContext,
                    out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            state = new ValidationState(targetResolution.GameObject!, targetResolution.Resource);
            return true;
        }

        private static bool TryValidateDeletionTarget (
            GameObject target,
            OperationResource resource,
            OperationExecutionContext executionContext,
            out string errorMessage)
        {
            if (resource.Kind != OperationTouchKind.Prefab)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (executionContext.TryGetTemporaryPrefabContentsRoot(resource.Path, out var temporaryPrefabContentsRoot)
                && temporaryPrefabContentsRoot != null
                && target == temporaryPrefabContentsRoot)
            {
                errorMessage = "Prefab root cannot be deleted.";
                return false;
            }

            if (PrefabOperationUtilities.TryGetOpenedPrefabStage(resource.Path, out var openedPrefabStage, out _))
            {
                var prefabContentsRoot = openedPrefabStage!.prefabContentsRoot;
                if (prefabContentsRoot != null
                    && target == prefabContentsRoot)
                {
                    errorMessage = "Prefab root cannot be deleted.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static void RegisterDeletedGlobalObjectIds (
            GameObject target,
            OperationResource resource,
            OperationExecutionContext executionContext)
        {
            RegisterDeletedGlobalObjectId(target, resource, executionContext);
            var components = target.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                RegisterDeletedGlobalObjectId(component, resource, executionContext);
            }

            var childCount = target.transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = target.transform.GetChild(i);
                RegisterDeletedGlobalObjectIds(child.gameObject, resource, executionContext);
            }
        }

        private static void RegisterDeletedGlobalObjectId (
            UnityEngine.Object unityObject,
            OperationResource resource,
            OperationExecutionContext executionContext)
        {
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(unityObject, out var directReference))
            {
                executionContext.MarkDeletedGlobalObjectId(directReference!.GlobalObjectId);
            }

            switch (resource.Kind)
            {
                case OperationTouchKind.Scene:
                    if (executionContext.TryResolveTemporarySceneSourceObject(resource.Path, unityObject, out var sourceSceneObject)
                        && sourceSceneObject != null
                        && UnityObjectReferenceResolver.TryCreateResolvedReference(sourceSceneObject, out var sourceSceneReference))
                    {
                        executionContext.MarkDeletedGlobalObjectId(sourceSceneReference!.GlobalObjectId);
                    }

                    break;

                case OperationTouchKind.Prefab:
                    if (executionContext.TryResolveTemporaryPrefabSourceObject(resource.Path, unityObject, out var sourcePrefabObject)
                        && sourcePrefabObject != null
                        && UnityObjectReferenceResolver.TryCreateResolvedReference(sourcePrefabObject, out var sourcePrefabReference))
                    {
                        executionContext.MarkDeletedGlobalObjectId(sourcePrefabReference!.GlobalObjectId);
                    }

                    break;
            }
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                OperationResource resource)
            {
                Target = target;
                Resource = resource;
            }

            public GameObject Target { get; }

            public OperationResource Resource { get; }
        }
    }
}
