using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.asset.set</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetSetOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""target"": {
                  ""type"": ""object"",
                  ""additionalProperties"": false,
                  ""properties"": {
                    ""var"": { ""type"": ""string"", ""minLength"": 1 },
                    ""globalObjectId"": { ""type"": ""string"", ""minLength"": 1 },
                    ""assetGuid"": { ""type"": ""string"", ""minLength"": 1 },
                    ""assetPath"": { ""type"": ""string"", ""minLength"": 1 }
                  },
                  ""oneOf"": [
                    { ""required"": [""var""] },
                    { ""required"": [""globalObjectId""] },
                    { ""required"": [""assetGuid""] },
                    { ""required"": [""assetPath""] }
                  ]
                },
                ""sets"": {
                  ""type"": ""array"",
                  ""minItems"": 1,
                  ""items"": {
                    ""type"": ""object"",
                    ""additionalProperties"": false,
                    ""properties"": {
                      ""path"": { ""type"": ""string"", ""minLength"": 1 },
                      ""value"": {}
                    },
                    ""required"": [""path"", ""value""]
                  }
                }
              },
              ""required"": [""target"", ""sets""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.asset.set",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryResolveValidateTarget(operation, executionContext, out _, out _, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanBinding(operation, executionContext, out var binding, out var sets, out var failure))
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
                allowTemporaryState: true,
                out var changed,
                out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed)
            {
                if (binding.SourceGlobalObjectId != null)
                {
                    executionContext.SetAssetShadow(binding.SourceGlobalObjectId, sandbox!, binding.AssetPath);
                }

                if (binding.Alias != null)
                {
                    executionContext.SetTemporaryAlias(
                        binding.Alias,
                        sandbox!,
                        new OperationResource(OperationTouchKind.Asset, binding.AssetPath),
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

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveCallBinding(operation, executionContext, out var binding, out var sets, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!SerializedObjectValueApplier.TryApply(
                binding.UnityObject,
                sets!,
                executionContext,
                allowTemporaryState: false,
                out var changed,
                out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: changed,
                touched: new[]
                {
                    AssetOperationUtilities.CreateAssetTouch(binding.AssetPath),
                }));
        }

        private static bool TryResolveValidateTarget (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? unityObject,
            out string assetPath,
            out SerializedObjectSetArguments? parsedArguments,
            out OperationPhaseStepResult? failure)
        {
            unityObject = null;
            assetPath = string.Empty;
            parsedArguments = null;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AssetOperationUtilities.TryResolveAssetTarget(
                arguments.TargetReference,
                executionContext,
                allowTemporaryState: true,
                out unityObject,
                out assetPath,
                out _,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            parsedArguments = arguments;
            return true;
        }

        private static bool TryResolvePlanBinding (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out TargetBinding binding,
            out System.Collections.Generic.IReadOnlyList<SerializedPropertyAssignment>? sets,
            out OperationPhaseStepResult? failure)
        {
            binding = default;
            sets = null;
            failure = null;
            if (!TryResolveValidateTarget(operation, executionContext, out _, out _, out var parsedArguments, out failure))
            {
                return false;
            }

            var targetReference = parsedArguments!.Value.TargetReference;
            var alias = targetReference.Kind == UnityObjectReferenceKind.Alias
                ? targetReference.Alias
                : null;
            if (alias != null
                && executionContext.TryGetTemporaryAliasState(alias, out var temporaryAliasState))
            {
                binding = new TargetBinding(
                    temporaryAliasState.UnityObject!,
                    temporaryAliasState.Resource.Path,
                    temporaryAliasState.SourceGlobalObjectId,
                    alias);
                sets = parsedArguments.Value.Sets;
                return true;
            }

            if (!AssetOperationUtilities.TryResolveAssetTarget(
                targetReference,
                executionContext,
                allowTemporaryState: false,
                out var resolvedAsset,
                out var assetPath,
                out var sourceGlobalObjectId,
                out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(sourceGlobalObjectId)
                && executionContext.TryGetAssetShadow(sourceGlobalObjectId, out var shadowAsset, out var shadowAssetPath))
            {
                binding = new TargetBinding(shadowAsset!, shadowAssetPath, sourceGlobalObjectId, alias);
            }
            else
            {
                binding = new TargetBinding(resolvedAsset!, assetPath, sourceGlobalObjectId, alias);
            }

            sets = parsedArguments.Value.Sets;
            return true;
        }

        private static bool TryResolveCallBinding (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out TargetBinding binding,
            out System.Collections.Generic.IReadOnlyList<SerializedPropertyAssignment>? sets,
            out OperationPhaseStepResult? failure)
        {
            binding = default;
            sets = null;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
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
                out _,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            binding = new TargetBinding(unityObject!, assetPath, sourceGlobalObjectId: null, alias: null);
            sets = arguments.Sets;
            return true;
        }

        private readonly struct TargetBinding
        {
            public TargetBinding (
                UnityEngine.Object unityObject,
                string assetPath,
                string? sourceGlobalObjectId,
                string? alias)
            {
                UnityObject = unityObject;
                AssetPath = assetPath;
                SourceGlobalObjectId = sourceGlobalObjectId;
                Alias = alias;
            }

            public UnityEngine.Object UnityObject { get; }

            public string AssetPath { get; }

            public string? SourceGlobalObjectId { get; }

            public string? Alias { get; }
        }
    }
}