using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.assets.find</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class AssetsFindOperation : UcliOperation<AssetsFindArgs, AssetsFindResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<AssetsFindArgs, AssetsFindResult>(
            operationName: UcliPrimitiveOperationNames.AssetsFind,
            kind: UcliOperationKind.Query,
            description: "Finds project assets by type, path prefix, or name substring.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate asset query arguments and observe matching project assets without applying mutation.",
                callSemantics: "Read matching project assets and emit bounded result data without applying mutation.",
                touchedContract: "Returns no touched resources because asset search results are data, not mutation targets.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Timeout, cancellation, or source read failure means the asset search was not fully produced.",
                dangerousNotes: Array.Empty<string>()));

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            AssetsFindArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryValidate(operation, args, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            AssetsFindArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(operation, args, executionContext, includeTemporaryState: true));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            AssetsFindArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(operation, args, executionContext, includeTemporaryState: false));
        }

        private static OperationPhaseStepResult Execute (
            NormalizedOperation operation,
            AssetsFindArgs args,
            OperationExecutionContext executionContext,
            bool includeTemporaryState)
        {
            if (!TryValidate(operation, args, out var validationState, out var failure))
            {
                return failure!;
            }

            var matches = includeTemporaryState
                ? AssetsFindSearchEngine.SearchWithTemporaryState(validationState.Criteria, executionContext)
                : AssetsFindSearchEngine.SearchLive(validationState.Criteria);
            var windowedMatches = BoundedWindowApplicator.Apply(matches, validationState.WindowOptions);
            var payloadMatches = new AssetsFindMatch[windowedMatches.Items.Count];
            for (var i = 0; i < windowedMatches.Items.Count; i++)
            {
                var match = windowedMatches.Items[i];
                payloadMatches[i] = new AssetsFindMatch(
                    assetPath: new UnityAssetPath(match.AssetPath),
                    assetGuid: match.AssetGuid == null ? null : new UnityAssetGuid(match.AssetGuid),
                    name: match.Name,
                    typeId: new UnityTypeId(match.TypeId));
            }

            return OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(new AssetsFindResult(payloadMatches, windowedMatches.Window)));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            AssetsFindArgs args,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            if (args.Type == null
                && args.PathPrefix == null
                && string.IsNullOrWhiteSpace(args.NameContains))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Operation 'args' must specify at least one of 'type', 'pathPrefix', or 'nameContains'.");
                return false;
            }

            Type? typeFilter = null;
            var typeId = args.Type?.Value;
            if (typeId != null)
            {
                if (!OperationRuntimeTypeResolver.TryResolveRuntimeType(typeId, out typeFilter, out var errorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                    return false;
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(typeFilter))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        $"TypeId must resolve to a UnityEngine.Object type: {typeId}.");
                    return false;
                }
            }

            var normalizedPathPrefix = args.PathPrefix?.Value;

            if (args.NameContains != null && string.IsNullOrWhiteSpace(args.NameContains))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Operation 'args.nameContains' must not be empty or whitespace.");
                return false;
            }

            var windowOptions = BoundedWindowOptionsNormalizer.NormalizeValidated(args.Limit, args.Cursor);

            validationState = new ValidationState(
                new AssetsFindSearchEngine.SearchCriteria(
                    typeFilter,
                    normalizedPathPrefix,
                    args.NameContains),
                windowOptions);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                AssetsFindSearchEngine.SearchCriteria criteria,
                BoundedWindowOptions windowOptions)
            {
                Criteria = criteria;
                WindowOptions = windowOptions;
            }

            public AssetsFindSearchEngine.SearchCriteria Criteria { get; }

            public BoundedWindowOptions WindowOptions { get; }
        }
    }
}
