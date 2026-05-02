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
    internal sealed class AssetsFindOperation : TypedUcliOperation<UcliOperationContracts.AssetsFindArgs, UcliOperationContracts.AssetsFindResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliOperationContracts.AssetsFindArgs, UcliOperationContracts.AssetsFindResult>(
            operationName: UcliPrimitiveOperationNames.AssetsFind,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe);

        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            UcliOperationContracts.AssetsFindArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryValidate(operation, args, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            UcliOperationContracts.AssetsFindArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(operation, args, executionContext, applied: false, includeTemporaryState: true));
        }

        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            UcliOperationContracts.AssetsFindArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(operation, args, executionContext, applied: true, includeTemporaryState: false));
        }

        private static OperationPhaseStepResult Execute (
            NormalizedOperation operation,
            UcliOperationContracts.AssetsFindArgs args,
            OperationExecutionContext executionContext,
            bool applied,
            bool includeTemporaryState)
        {
            if (!TryValidate(operation, args, out var validationState, out var failure))
            {
                return failure!;
            }

            var matches = includeTemporaryState
                ? AssetsFindSearchEngine.SearchWithTemporaryState(validationState.Criteria, executionContext)
                : AssetsFindSearchEngine.SearchLive(validationState.Criteria);
            var payloadMatches = new UcliOperationContracts.AssetsFindMatch[matches.Count];
            for (var i = 0; i < matches.Count; i++)
            {
                payloadMatches[i] = new UcliOperationContracts.AssetsFindMatch(
                    assetPath: matches[i].AssetPath,
                    assetGuid: matches[i].AssetGuid,
                    name: matches[i].Name,
                    typeId: matches[i].TypeId);
            }

            return OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(new UcliOperationContracts.AssetsFindResult(payloadMatches)));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            UcliOperationContracts.AssetsFindArgs args,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            Type? typeFilter = null;
            if (args.Type != null)
            {
                if (!OperationRuntimeTypeResolver.TryResolveRuntimeType(args.Type, out typeFilter, out var errorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                    return false;
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(typeFilter))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        $"TypeId must resolve to a UnityEngine.Object type: {args.Type}.");
                    return false;
                }
            }

            string? normalizedPathPrefix = null;
            if (args.PathPrefix != null)
            {
                normalizedPathPrefix = UnityAssetPathUtility.NormalizeAssetPath(args.PathPrefix);
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
                    args.NameContains));
            return true;
        }

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
