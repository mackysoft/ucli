using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Project;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.assets.find</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetsFindOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""type"": { ""type"": ""string"", ""minLength"": 1 },
                ""pathPrefix"": { ""type"": ""string"", ""minLength"": 1 },
                ""nameContains"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""minProperties"": 1
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.AssetsFind,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryValidate(operation, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(operation, executionContext, applied: false, includeTemporaryState: true));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(operation, executionContext, applied: true, includeTemporaryState: false));
        }

        private static OperationPhaseStepResult Execute (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied,
            bool includeTemporaryState)
        {
            if (!TryValidate(operation, out var validationState, out var failure))
            {
                return failure!;
            }

            var matches = includeTemporaryState
                ? AssetsFindSearchEngine.SearchWithTemporaryState(validationState.Criteria, executionContext)
                : AssetsFindSearchEngine.SearchLive(validationState.Criteria);
            var payloadMatches = new AssetsFindMatchPayload[matches.Count];
            for (var i = 0; i < matches.Count; i++)
            {
                payloadMatches[i] = new AssetsFindMatchPayload(
                    AssetPath: matches[i].AssetPath,
                    AssetGuid: matches[i].AssetGuid,
                    Name: matches[i].Name,
                    TypeId: matches[i].TypeId);
            }

            return OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(new AssetsFindResult(payloadMatches)));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!AssetsFindArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            Type? typeFilter = null;
            if (arguments.TypeId != null)
            {
                if (!OperationRuntimeTypeResolver.TryResolveRuntimeType(arguments.TypeId, out typeFilter, out errorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                    return false;
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(typeFilter))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        $"TypeId must resolve to a UnityEngine.Object type: {arguments.TypeId}.");
                    return false;
                }
            }

            string? normalizedPathPrefix = null;
            if (arguments.PathPrefix != null)
            {
                normalizedPathPrefix = UnityAssetPathUtility.NormalizeAssetPath(arguments.PathPrefix);
                if (!UnityAssetPathUtility.IsAssetsRootOrDescendant(normalizedPathPrefix))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        $"Path prefix must be 'Assets' or one of its descendants. Actual: {normalizedPathPrefix}.");
                    return false;
                }
            }

            validationState = new ValidationState(
                new AssetsFindSearchEngine.SearchCriteria(
                    typeFilter,
                    normalizedPathPrefix,
                    arguments.NameContains));
            return true;
        }

        private sealed record AssetsFindResult (IReadOnlyList<AssetsFindMatchPayload> Matches);

        private sealed record AssetsFindMatchPayload (
            string AssetPath,
            string AssetGuid,
            string Name,
            string TypeId);

        private readonly struct ValidationState
        {
            public ValidationState (AssetsFindSearchEngine.SearchCriteria criteria)
            {
                Criteria = criteria;
            }

            public AssetsFindSearchEngine.SearchCriteria Criteria { get; }
        }
    }
}
