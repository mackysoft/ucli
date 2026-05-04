using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.asset.set</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetSetOperation : UcliOperation<AssetSetArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<AssetSetArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.AssetSet,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            description: "Assigns serialized property values on an asset or project asset target.",
            assurance: new UcliOperationAssuranceContract(
                new[] { UcliOperationSideEffect.WritesAsset, UcliOperationSideEffect.WritesProjectSettings },
                mayDirty: true,
                mayPersist: false,
                new[] { IpcExecuteTouchedResourceKindNames.Asset, IpcExecuteTouchedResourceKindNames.ProjectSettings },
                UcliOperationPlanMode.MayCreatePreviewState));

        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            AssetSetArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryResolveValidateTarget(operation, args, executionContext, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            AssetSetArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanBinding(operation, args, executionContext, out var binding, out var sets, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!AssetOperationUtilities.TryCreateTemporaryAssetClone(
                binding.UnityObject,
                executionContext,
                out var sandbox,
                out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            if (!SerializedObjectValueApplier.TryApply(
                sandbox!,
                sets!,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState,
                operation.AllowRequestLocalAliases,
                out var changed,
                out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed)
            {
                executionContext.MarkRequestAttributedChange(OperationResource.PersistentAsset(binding.AssetPath));
                if (binding.SourceGlobalObjectId != null)
                {
                    executionContext.SetAssetShadow(binding.SourceGlobalObjectId, sandbox!, binding.AssetPath);
                }

                if (binding.PlannedOwnerExecutionKey != null)
                {
                    executionContext.SetPlannedAsset(binding.AssetPath, binding.PlannedOwnerExecutionKey, sandbox!);
                }

                if (binding.Alias != null)
                {
                    executionContext.SetTemporaryAlias(
                        binding.Alias,
                        sandbox!,
                        OperationResource.PersistentAsset(binding.AssetPath),
                        binding.SourceGlobalObjectId);
                }
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: changed,
                touched: new[]
                {
                    AssetOperationUtilities.CreateAssetTouch(binding.AssetPath),
                }));
        }

        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            AssetSetArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveCallBinding(operation, args, executionContext, out var binding, out var sets, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!AssetOperationUtilities.TryCreateTemporaryAssetClone(
                binding.UnityObject,
                executionContext,
                out var sandbox,
                out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            if (!SerializedObjectValueApplier.TryApply(
                sandbox!,
                sets!,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryAliases,
                operation.AllowRequestLocalAliases,
                out var changed,
                out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed
                && !AssetOperationUtilities.TryCopySerializedState(sandbox!, binding.UnityObject, out var copyErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Validated asset mutation could not be committed. {copyErrorMessage}",
                    OpId: operation.Id)));
            }

            if (changed)
            {
                executionContext.MarkRequestAttributedChange(OperationResource.PersistentAsset(binding.AssetPath));
                EditorUtility.SetDirty(binding.UnityObject);
            }

            if (binding.Alias != null
                && UnityObjectReferenceResolver.TryCreateResolvedReference(binding.UnityObject, out var resolvedReference))
            {
                executionContext.SetTemporaryAlias(
                    binding.Alias,
                    binding.UnityObject,
                    OperationResource.PersistentAsset(binding.AssetPath),
                    resolvedReference!.GlobalObjectId);
                executionContext.AliasStore.Set(binding.Alias, resolvedReference);
            }

            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: changed,
                    touched: new[]
                    {
                        AssetOperationUtilities.CreateAssetTouch(binding.AssetPath),
                    })
                .WithReadInvalidations(
                    changed
                        ? OperationReadInvalidationUtilities.CreateAssetSearchOnly()
                        : null));
        }

        private static bool TryResolveValidateTarget (
            NormalizedOperation operation,
            AssetSetArgs args,
            OperationExecutionContext executionContext,
            out ValidatedTargetState validatedTargetState,
            out OperationPhaseStepResult? failure)
        {
            validatedTargetState = default;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            UnityEngine.Object? unityObject;
            string assetPath;
            if (!AssetOperationUtilities.TryResolveAssetTarget(
                arguments.TargetReference,
                executionContext,
                allowTemporaryState: true,
                out unityObject,
                out assetPath,
                out var sourceGlobalObjectId,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            var plannedOwnerExecutionKey =
                string.IsNullOrWhiteSpace(sourceGlobalObjectId)
                && executionContext.TryGetPlannedAssetState(assetPath, out var plannedAssetState)
                    ? plannedAssetState.OwnerExecutionKey
                    : null;
            validatedTargetState = new ValidatedTargetState(
                arguments,
                unityObject!,
                assetPath,
                sourceGlobalObjectId,
                plannedOwnerExecutionKey);
            return true;
        }

        private static bool TryResolvePlanBinding (
            NormalizedOperation operation,
            AssetSetArgs args,
            OperationExecutionContext executionContext,
            out TargetBinding binding,
            out IReadOnlyList<SerializedPropertyAssignment>? sets,
            out OperationPhaseStepResult? failure)
        {
            binding = default;
            sets = null;
            failure = null;
            if (!TryResolveValidateTarget(operation, args, executionContext, out var validatedTargetState, out failure))
            {
                return false;
            }

            var targetReference = validatedTargetState.ParsedArguments.TargetReference;
            var alias = targetReference.Kind == UnityObjectReferenceKind.Alias
                ? targetReference.Alias
                : null;

            if (!string.IsNullOrWhiteSpace(validatedTargetState.SourceGlobalObjectId)
                && executionContext.TryGetAssetShadow(validatedTargetState.SourceGlobalObjectId, out var shadowAsset, out var shadowAssetPath))
            {
                binding = new TargetBinding(
                    shadowAsset!,
                    shadowAssetPath,
                    validatedTargetState.SourceGlobalObjectId,
                    alias,
                    plannedOwnerExecutionKey: null);
            }
            else
            {
                binding = new TargetBinding(
                    validatedTargetState.UnityObject,
                    validatedTargetState.AssetPath,
                    validatedTargetState.SourceGlobalObjectId,
                    alias,
                    validatedTargetState.PlannedOwnerExecutionKey);
            }

            sets = validatedTargetState.ParsedArguments.Sets;
            return true;
        }

        private static bool TryResolveCallBinding (
            NormalizedOperation operation,
            AssetSetArgs args,
            OperationExecutionContext executionContext,
            out TargetBinding binding,
            out IReadOnlyList<SerializedPropertyAssignment>? sets,
            out OperationPhaseStepResult? failure)
        {
            binding = default;
            sets = null;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AssetOperationUtilities.TryResolveAssetTarget(
                arguments.TargetReference,
                executionContext,
                allowTemporaryState: false,
                out var unityObject,
                out var assetPath,
                out var sourceGlobalObjectId,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            var alias = arguments.TargetReference.Kind == UnityObjectReferenceKind.Alias
                ? arguments.TargetReference.Alias
                : null;
            binding = new TargetBinding(unityObject!, assetPath, sourceGlobalObjectId, alias, plannedOwnerExecutionKey: null);
            sets = arguments.Sets;
            return true;
        }

        private readonly struct ValidatedTargetState
        {
            public ValidatedTargetState (
                SerializedObjectSetArguments parsedArguments,
                UnityEngine.Object unityObject,
                string assetPath,
                string? sourceGlobalObjectId,
                string? plannedOwnerExecutionKey)
            {
                ParsedArguments = parsedArguments;
                UnityObject = unityObject;
                AssetPath = assetPath;
                SourceGlobalObjectId = sourceGlobalObjectId;
                PlannedOwnerExecutionKey = plannedOwnerExecutionKey;
            }

            public SerializedObjectSetArguments ParsedArguments { get; }

            public UnityEngine.Object UnityObject { get; }

            public string AssetPath { get; }

            public string? SourceGlobalObjectId { get; }

            public string? PlannedOwnerExecutionKey { get; }
        }

        private readonly struct TargetBinding
        {
            public TargetBinding (
                UnityEngine.Object unityObject,
                string assetPath,
                string? sourceGlobalObjectId,
                string? alias,
                string? plannedOwnerExecutionKey)
            {
                UnityObject = unityObject;
                AssetPath = assetPath;
                SourceGlobalObjectId = sourceGlobalObjectId;
                Alias = alias;
                PlannedOwnerExecutionKey = plannedOwnerExecutionKey;
            }

            public UnityEngine.Object UnityObject { get; }

            public string AssetPath { get; }

            public string? SourceGlobalObjectId { get; }

            public string? Alias { get; }

            public string? PlannedOwnerExecutionKey { get; }
        }
    }
}
