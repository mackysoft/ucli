using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.prefab.create</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class PrefabCreateOperation : UcliOperation<PrefabCreateArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<PrefabCreateArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.PrefabCreate,
            kind: UcliOperationKind.Mutation,
            description: "Creates a prefab asset from a scene GameObject.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[]
                {
                    UcliOperationSideEffect.SceneContentMutation,
                    UcliOperationSideEffect.PrefabContentMutation,
                    UcliOperationSideEffect.PrefabSave,
                },
                touchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Scene, IpcExecuteTouchedResourceKindNames.Prefab },
                planMode: UcliOperationPlanMode.MayCreatePreviewState,
                planSemantics: "Validate the source GameObject and prefab path, then compute preview creation state without persisting project data.",
                callSemantics: "Create and persist the prefab asset from the live GameObject, and dirty related scene state when Unity creates prefab linkage.",
                touchedContract: "Reports the created prefab and any scene resource dirtied by prefab creation when Unity exposes them.",
                readPostconditionContract: "Prefab, scene, asset search, GUID path, and readIndex surfaces covering touched resources may be stale after a successful call.",
                failureSemantics: "Prefab creation is not transactional; timeout, cancellation, or Unity failure can leave partial or indeterminate scene and prefab file changes.",
                dangerousNotes: new[] { "This operation can persist prefab project files and dirty scene state; callers must account for partial Unity save/import behavior." }),
            exposure: UcliOperationExposure.EditLoweringOnly);

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            PrefabCreateArgs args,
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

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            PrefabCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(
                operation,
                args,
                executionContext,
                allowTemporaryState: true,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsurePlanResourceState(
                    validationState.SourceResource,
                    executionContext,
                    out var preparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    preparationErrorMessage));
            }

            if (!TryValidateArguments(
                    operation,
                    args,
                    executionContext,
                    allowTemporaryState: true,
                    out validationState,
                    out failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                    validationState.Target!,
                    validationState.SourceResource,
                    executionContext,
                    out var projectionErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    projectionErrorMessage));
            }

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(operation.As, validationState.Target, validationState.SourceResource);
            }

            executionContext.MarkRequestAttributedChange(validationState.SourceResource);
            executionContext.TrackPlannedPrefabCreation(validationState.Target!, validationState.PrefabPath);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: OperationResourceUtilities.CreateTouches(
                    validationState.SourceResource,
                    new OperationResource(OperationTouchKind.Prefab, validationState.PrefabPath))));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            PrefabCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(
                operation,
                args,
                executionContext,
                allowTemporaryState: false,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(validationState.Target, validationState.PrefabPath, InteractionMode.AutomatedAction);
            if (prefabAsset == null)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab could not be created: {validationState.PrefabPath}."));
            }

            executionContext.MarkRequestAttributedChange(validationState.SourceResource);
            StoreAliasIfNeeded(operation.As, executionContext, validationState.Target, validationState.SourceResource);
            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: OperationResourceUtilities.CreateTouches(
                        validationState.SourceResource,
                        new OperationResource(OperationTouchKind.Prefab, validationState.PrefabPath)))
                .WithPersistence()
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateAssetSearchAndGuidPath()));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            PrefabCreateArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
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

            if (targetResolution.Resource.Kind != OperationTouchKind.Scene)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Prefab source must be a GameObject that belongs to a loaded scene.");
                return false;
            }

            if (!PrefabOperationUtilities.TryEnsurePrefabAssetCanBeCreated(args.Path, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validationState = new ValidationState(
                targetResolution.GameObject!,
                targetResolution.Resource,
                args.Path);
            return true;
        }

        private static void StoreAliasIfNeeded (
            string? alias,
            OperationExecutionContext executionContext,
            GameObject target,
            OperationResource resource)
        {
            if (alias == null)
            {
                return;
            }

            executionContext.SetTemporaryAlias(alias, target, resource);
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(target, out var resolvedReference))
            {
                executionContext.AliasStore.Set(alias, resolvedReference!);
            }
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                OperationResource sourceResource,
                string prefabPath)
            {
                Target = target;
                SourceResource = sourceResource;
                PrefabPath = prefabPath;
            }

            public GameObject? Target { get; }

            public OperationResource SourceResource { get; }

            public string PrefabPath { get; }
        }
    }
}
