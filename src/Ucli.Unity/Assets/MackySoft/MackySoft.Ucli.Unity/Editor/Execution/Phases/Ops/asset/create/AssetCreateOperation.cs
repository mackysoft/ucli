using System;
using MackySoft.Ucli.Contracts;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.asset.create</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetCreateOperation : UcliOperation<AssetCreateArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<AssetCreateArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.AssetCreate,
            kind: UcliOperationKind.Mutation,
            description: "Creates a Unity asset at a project-relative path.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.AssetContentMutation, UcliOperationSideEffect.AssetSave },
                touchedKinds: new[] { UcliTouchedResourceKindNames.Asset },
                planMode: UcliOperationPlanMode.MayCreatePreviewState,
                planSemantics: "Validate the asset creation target and compute preview creation state without persisting project data.",
                callSemantics: "Create and persist the requested asset at the project-relative path.",
                touchedContract: "Reports the created asset resource when Unity returns a persisted asset path.",
                readPostconditionContract: "Asset search, GUID path, schema, and readIndex surfaces covering the created asset may be stale after a successful call.",
                failureSemantics: "Asset creation is not transactional; timeout, cancellation, or Unity failure can leave partial or indeterminate asset file changes.",
                dangerousNotes: new[] { "This operation can create project files and is not transactional across Unity asset creation/import steps." }),
            exposure: UcliOperationExposure.EditLoweringOnly);

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            AssetCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, out _, out var assetPath, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(TryValidatePlannedAssetPath(operation, executionContext, assetPath!, out failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            AssetCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, out var assetType, out var assetPath, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!TryValidatePlannedAssetPath(operation, executionContext, assetPath!, out failure))
            {
                return Task.FromResult(failure!);
            }

            UnityEngine.Object? temporaryAsset = null;
            if (executionContext.TryGetPlannedAssetState(assetPath!, out var plannedAssetState))
            {
                temporaryAsset = plannedAssetState.UnityObject;
            }
            else if (!AssetOperationUtilities.TryCreateTemporaryAssetInstance(
                assetType!,
                executionContext,
                out temporaryAsset,
                out var errorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: UcliCoreErrorCodes.InternalError,
                    Message: errorMessage,
                    OpId: operation.Id)));
            }

            executionContext.SetPlannedAsset(assetPath!, operation.EffectiveExecutionKey, temporaryAsset!);
            executionContext.MarkRequestAttributedChange(OperationResource.PersistentAsset(assetPath!));
            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(
                    operation.As,
                    temporaryAsset!,
                    OperationResource.PersistentAsset(assetPath!));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: new[]
                {
                    AssetOperationUtilities.CreateAssetTouch(assetPath!),
                }));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            AssetCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, out var assetType, out var assetPath, out var failure))
            {
                return Task.FromResult(failure!);
            }

            ScriptableObject? asset = null;
            var created = false;
            try
            {
                asset = ScriptableObject.CreateInstance(assetType!);
                if (asset == null)
                {
                    return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: UcliCoreErrorCodes.InternalError,
                        Message: $"Asset instance could not be created for type '{assetType!.FullName}'.",
                        OpId: operation.Id)));
                }

                AssetDatabase.CreateAsset(asset, assetPath!);
                created = true;
                executionContext.MarkRequestAttributedChange(OperationResource.PersistentAsset(assetPath!));
                if (operation.As != null)
                {
                    if (UnityObjectReferenceResolver.TryCreateResolvedReference(asset, out var resolvedReference))
                    {
                        executionContext.SetTemporaryAlias(
                            operation.As,
                            asset,
                            OperationResource.PersistentAsset(assetPath!),
                            resolvedReference!.GlobalObjectId);
                        executionContext.AliasStore.Set(operation.As, resolvedReference);
                    }
                    else
                    {
                        executionContext.SetTemporaryAlias(
                            operation.As,
                            asset,
                            OperationResource.PersistentAsset(assetPath!));
                    }
                }

                return Task.FromResult(
                    OperationPhaseStepResult.Success(
                        applied: true,
                        changed: true,
                        touched: new[]
                        {
                            AssetOperationUtilities.CreateAssetTouch(assetPath!),
                        })
                    .WithPersistence()
                    .WithReadInvalidations(OperationReadInvalidationUtilities.CreateAssetSearchAndGuidPath()));
            }
            finally
            {
                if (!created && asset != null)
                {
                    ScriptableObject.DestroyImmediate(asset);
                }
            }
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            AssetCreateArgs args,
            out Type? assetType,
            out string? assetPath,
            out OperationPhaseStepResult? failure)
        {
            assetType = null;
            assetPath = null;
            failure = null;

            if (!AssetTypeResolver.TryResolveCreateAssetType(args.Type, out assetType, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AssetOperationUtilities.TryValidateCreateAssetPath(args.Path, out assetPath, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            return true;
        }

        private static bool TryValidatePlannedAssetPath (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            string assetPath,
            out OperationPhaseStepResult? failure)
        {
            failure = null;
            if (!executionContext.TryGetPlannedAssetState(assetPath, out var plannedAssetState))
            {
                return true;
            }

            if (string.Equals(plannedAssetState.OwnerExecutionKey, operation.EffectiveExecutionKey, StringComparison.Ordinal))
            {
                return true;
            }

            failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                operation.Id,
                $"Asset path is already reserved by another planned create operation: {plannedAssetState.AssetPath}.");
            return false;
        }
    }
}
