using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.asset.schema</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetSchemaOperation : TypedUcliOperation<UcliOperationContracts.AssetSchemaArgs, IndexSchemaEntryJsonContract>
    {
        private readonly AssetSchemaExtractor assetSchemaExtractor =
            new AssetSchemaExtractor(new IndexSchemaPropertyCollector());

        private readonly AssetTargetSchemaBuilder targetSchemaBuilder = new AssetTargetSchemaBuilder();

        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliOperationContracts.AssetSchemaArgs, IndexSchemaEntryJsonContract>(
            operationName: UcliPrimitiveOperationNames.AssetSchema,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe);

        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            UcliOperationContracts.AssetSchemaArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryValidate(operation, args, executionContext, allowTemporaryState: true, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        protected override async Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            UcliOperationContracts.AssetSchemaArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(
                operation,
                args,
                executionContext,
                applied: false,
                allowTemporaryState: true,
                cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            UcliOperationContracts.AssetSchemaArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(
                operation,
                args,
                executionContext,
                applied: true,
                allowTemporaryState: false,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            UcliOperationContracts.AssetSchemaArgs args,
            OperationExecutionContext executionContext,
            bool applied,
            bool allowTemporaryState,
            CancellationToken cancellationToken)
        {
            if (!TryValidate(operation, args, executionContext, allowTemporaryState, out var validationState, out var failure))
            {
                return failure!;
            }

            if (validationState.AssetType != null)
            {
                var extractionResult = await assetSchemaExtractor.Extract(
                    new[] { validationState.AssetType },
                    cancellationToken).ConfigureAwait(false);
                if (extractionResult.Entries.Count == 0)
                {
                    return OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: $"Schema could not be extracted for type '{validationState.AssetType.FullName}'.",
                        OpId: operation.Id));
                }

                return OperationPhaseStepResult.Success(
                    applied: applied,
                    changed: false,
                    result: IpcPayloadCodec.SerializeToElement(extractionResult.Entries[0]));
            }

            return OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(validationState.TargetSchemaEntry));
        }

        private bool TryValidate (
            NormalizedOperation operation,
            UcliOperationContracts.AssetSchemaArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            if (args.Target == null)
            {
                if (!AssetTypeResolver.TryResolveCreateAssetType(args.Type!, out var assetType, out var typeErrorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, typeErrorMessage);
                    return false;
                }

                validationState = new ValidationState(assetType!, null);
                return true;
            }

            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var targetReference, out var errorMessage)
                || !TryResolveTargetAsset(targetReference, executionContext, allowTemporaryState, out var unityObject, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validationState = new ValidationState(null, targetSchemaBuilder.Build(unityObject!));
            return true;
        }

        private static bool TryResolveTargetAsset (
            UnityObjectReference targetReference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!AssetOperationUtilities.TryResolveAssetTarget(
                targetReference,
                executionContext,
                allowTemporaryState,
                out var resolvedAsset,
                out _,
                out var sourceGlobalObjectId,
                out errorMessage))
            {
                return false;
            }

            if (allowTemporaryState
                && !string.IsNullOrWhiteSpace(sourceGlobalObjectId)
                && executionContext.TryGetAssetShadow(sourceGlobalObjectId, out var shadowAsset, out _))
            {
                unityObject = shadowAsset;
                return true;
            }

            unityObject = resolvedAsset;
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                Type? assetType,
                IndexSchemaEntryJsonContract? targetSchemaEntry)
            {
                AssetType = assetType;
                TargetSchemaEntry = targetSchemaEntry;
            }

            public Type? AssetType { get; }

            public IndexSchemaEntryJsonContract? TargetSchemaEntry { get; }
        }
    }
}
