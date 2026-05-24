using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.asset.save</c> operation flow for edit-lowered target-limited persistence. </summary>
    [UcliOperation]
    internal sealed class AssetSaveOperation : UcliOperation<AssetSaveArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<AssetSaveArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.AssetSave,
            kind: UcliOperationKind.Mutation,
            description: "Saves one request-attributed asset or ProjectSettings asset target.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.AssetSave },
                touchedKinds: new[]
                {
                    UcliTouchedResourceKindNames.Asset,
                    UcliTouchedResourceKindNames.ProjectSettings,
                },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the asset target and observe whether this request dirtied the target.",
                callSemantics: "Persist only the selected asset or ProjectSettings asset when it has request-attributed changes.",
                touchedContract: "Reports the selected asset or ProjectSettings asset when it is the save target.",
                readPostconditionContract: "Asset, ProjectSettings, GUID path, and readIndex surfaces covering the saved target may be stale after a successful call.",
                failureSemantics: "Asset save is not transactional; timeout, cancellation, or Unity failure can leave partial or indeterminate file changes.",
                dangerousNotes: new[] { "This operation can persist one asset or ProjectSettings file and is not transactional." }),
            exposure: UcliOperationExposure.EditLoweringOnly);

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            AssetSaveArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryResolveTarget(operation, args, executionContext, allowTemporaryState: true, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            AssetSaveArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveTarget(operation, args, executionContext, allowTemporaryState: true, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(state.Resource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: hasRequestAttributedChange,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(state.Resource),
                }));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            AssetSaveArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveTarget(operation, args, executionContext, allowTemporaryState: false, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(state.Resource);
            if (!hasRequestAttributedChange)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(state.Resource),
                    }));
            }

            AssetDatabase.SaveAssetIfDirty(state.UnityObject);
            executionContext.UnmarkRequestAttributedChange(state.Resource);

            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(state.Resource),
                    })
                .WithPersistence()
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateForProjectSave(
                    new[]
                    {
                        OperationResourceUtilities.CreateTouch(state.Resource),
                    })));
        }

        private static bool TryResolveTarget (
            NormalizedOperation operation,
            AssetSaveArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out SaveTargetState state,
            out OperationPhaseStepResult? failure)
        {
            state = default;
            failure = null;
            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var reference, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AssetOperationUtilities.TryResolveAssetTarget(
                reference,
                executionContext,
                allowTemporaryState,
                out var unityObject,
                out var assetPath,
                out _,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            state = new SaveTargetState(
                unityObject!,
                OperationResource.PersistentAsset(assetPath));
            return true;
        }

        private readonly struct SaveTargetState
        {
            public SaveTargetState (
                UnityEngine.Object unityObject,
                OperationResource resource)
            {
                UnityObject = unityObject;
                Resource = resource;
            }

            public UnityEngine.Object UnityObject { get; }

            public OperationResource Resource { get; }
        }
    }
}
