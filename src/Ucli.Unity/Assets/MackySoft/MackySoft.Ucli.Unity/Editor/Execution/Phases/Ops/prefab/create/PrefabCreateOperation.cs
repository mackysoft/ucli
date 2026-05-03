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
    internal sealed class PrefabCreateOperation : TypedUcliOperation<UcliOperationContracts.PrefabCreateArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliOperationContracts.PrefabCreateArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.PrefabCreate,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            describeContract: UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.PrefabCreate));

        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            UcliOperationContracts.PrefabCreateArgs args,
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

        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            UcliOperationContracts.PrefabCreateArgs args,
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

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(operation.As, validationState.Target, validationState.SourceResource);
            }

            executionContext.MarkRequestAttributedChange(validationState.SourceResource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: OperationResourceUtilities.CreateTouches(
                    validationState.SourceResource,
                    new OperationResource(OperationTouchKind.Prefab, validationState.PrefabPath))));
        }

        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            UcliOperationContracts.PrefabCreateArgs args,
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
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateAssetSearchAndGuidPath()));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            UcliOperationContracts.PrefabCreateArgs args,
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
