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
    /// <summary> Implements <c>ucli.asset.create</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetCreateOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""type"": { ""type"": ""string"", ""minLength"": 1 },
                ""path"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""type"", ""path""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.AssetCreate,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, out _, out var assetPath, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(TryValidatePlannedAssetPath(operation, executionContext, assetPath!, out failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, out var assetType, out var assetPath, out var failure))
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
                    Code: IpcErrorCodes.InternalError,
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

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, out var assetType, out var assetPath, out var failure))
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
                        Code: IpcErrorCodes.InternalError,
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

                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: new[]
                    {
                        AssetOperationUtilities.CreateAssetTouch(assetPath!),
                    },
                    readInvalidations: OperationReadInvalidationUtilities.CreateAssetSearchAndGuidPath()));
            }
            finally
            {
                if (!created && asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            out System.Type? assetType,
            out string? assetPath,
            out OperationPhaseStepResult? failure)
        {
            assetType = null;
            assetPath = null;
            failure = null;
            if (!AssetCreateArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AssetTypeResolver.TryResolveCreateAssetType(parsedArguments.TypeId, out assetType, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AssetOperationUtilities.TryValidateCreateAssetPath(parsedArguments.AssetPath, out assetPath, out errorMessage))
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

            if (string.Equals(plannedAssetState.OwnerExecutionKey, operation.EffectiveExecutionKey, System.StringComparison.Ordinal))
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
