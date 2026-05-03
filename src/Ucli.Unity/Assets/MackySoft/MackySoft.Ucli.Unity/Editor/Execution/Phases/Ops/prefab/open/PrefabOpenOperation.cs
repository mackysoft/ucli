using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.prefab.open</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class PrefabOpenOperation : TypedUcliOperation<UcliOperationContracts.PathArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliOperationContracts.PathArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.PrefabOpen,
            kind: UcliOperationKind.Command,
            policy: OperationPolicy.Safe,
            describeContract: UcliOperationDescribeCatalog.Get(UcliPrimitiveOperationNames.PrefabOpen));

        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            UcliOperationContracts.PathArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            UcliOperationContracts.PathArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(validationState.PrefabPath, out _, out _)
                && !PrefabOperationUtilities.TryEnsureCanOpenPrefabStage(validationState.PrefabPath, out var blockerErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    blockerErrorMessage));
            }

            GameObject? prefabContentsRoot;
            if (!executionContext.TryGetOrCreateTemporaryPrefabContentsRoot(
                    validationState.PrefabPath,
                    out prefabContentsRoot,
                    out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(
                    operation.As,
                    prefabContentsRoot!,
                    new OperationResource(OperationTouchKind.Prefab, validationState.PrefabPath));
            }

            executionContext.TrackPlannedLivePrefabOpen(validationState.PrefabPath);

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Prefab, validationState.PrefabPath)),
                }));
        }

        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            UcliOperationContracts.PathArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!PrefabOperationUtilities.TryOpenPrefabStage(validationState.PrefabPath, out var prefabStage, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            var prefabContentsRoot = prefabStage!.prefabContentsRoot;
            if (prefabContentsRoot == null)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab root is not available after open: {validationState.PrefabPath}."));
            }

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(
                    operation.As,
                    prefabContentsRoot,
                    new OperationResource(OperationTouchKind.Prefab, validationState.PrefabPath));
                if (UnityObjectReferenceResolver.TryCreateResolvedReference(prefabContentsRoot, out var resolvedReference))
                {
                    executionContext.AliasStore.Set(operation.As, resolvedReference!);
                }
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Prefab, validationState.PrefabPath)),
                }));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            UcliOperationContracts.PathArgs args,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            if (!PrefabOperationUtilities.TryEnsurePrefabAssetExists(args.Path, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validationState = new ValidationState(args.Path);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (string prefabPath)
            {
                PrefabPath = prefabPath;
            }

            public string PrefabPath { get; }
        }
    }
}
