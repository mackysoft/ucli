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
            operationName: "ucli.asset.create",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryValidate(operation, out _, out _, out var failure)
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

            UnityEngine.Object? temporaryAsset = null;
            if (operation.As != null
                && !AssetOperationUtilities.TryCreateTemporaryAssetInstance(
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

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(
                    operation.As,
                    temporaryAsset!,
                    new OperationResource(OperationTouchKind.Asset, assetPath!));
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
                if (operation.As != null)
                {
                    executionContext.AliasStore.Set(operation.As, UnityObjectReferenceResolver.CreateResolvedReference(asset));
                }

                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: new[]
                    {
                        AssetOperationUtilities.CreateAssetTouch(assetPath!),
                    }));
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
    }
}