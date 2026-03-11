using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.asset.schema</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetSchemaOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""type"": { ""type"": ""string"", ""minLength"": 1 },
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
                }
              },
              ""oneOf"": [
                { ""required"": [""type""] },
                { ""required"": [""target""] }
              ]
            }";

        private readonly AssetSchemaExtractor assetSchemaExtractor =
            new AssetSchemaExtractor(new IndexSchemaPropertyCollector());

        private readonly AssetTargetSchemaBuilder targetSchemaBuilder = new AssetTargetSchemaBuilder();

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.asset.schema",
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryValidate(operation, executionContext, allowTemporaryState: true, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        public async Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(
                operation,
                executionContext,
                applied: false,
                allowTemporaryState: true,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Execute(
                operation,
                executionContext,
                applied: true,
                allowTemporaryState: false,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied,
            bool allowTemporaryState,
            CancellationToken cancellationToken)
        {
            if (!TryValidate(operation, executionContext, allowTemporaryState, out var validationState, out var failure))
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
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!AssetSchemaArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!arguments.HasTargetReference)
            {
                if (!AssetTypeResolver.TryResolveCreateAssetType(arguments.TypeId!, out var assetType, out errorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                    return false;
                }

                validationState = new ValidationState(assetType!, null);
                return true;
            }

            if (!TryResolveTargetAsset(arguments, executionContext, allowTemporaryState, out var unityObject, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validationState = new ValidationState(null, targetSchemaBuilder.Build(unityObject!));
            return true;
        }

        private static bool TryResolveTargetAsset (
            AssetSchemaArguments arguments,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!AssetOperationUtilities.TryResolveAssetTarget(
                arguments.TargetReference,
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
                System.Type? assetType,
                MackySoft.Ucli.Contracts.Index.IndexSchemaEntryJsonContract? targetSchemaEntry)
            {
                AssetType = assetType;
                TargetSchemaEntry = targetSchemaEntry;
            }

            public System.Type? AssetType { get; }

            public MackySoft.Ucli.Contracts.Index.IndexSchemaEntryJsonContract? TargetSchemaEntry { get; }
        }
    }
}
