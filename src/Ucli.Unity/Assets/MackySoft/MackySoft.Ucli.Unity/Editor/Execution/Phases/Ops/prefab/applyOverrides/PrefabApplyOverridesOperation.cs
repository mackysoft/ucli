using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
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
    /// <summary> Implements <c>ucli.prefab.applyOverrides</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class PrefabApplyOverridesOperation : UcliOperation<PrefabOverrideArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<PrefabOverrideArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
            kind: UcliOperationKind.Mutation,
            description: "Applies request-attributed Prefab instance property overrides to an explicit Prefab asset.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.PrefabContentMutation, UcliOperationSideEffect.PrefabSave },
                touchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Prefab },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate the request-attributed property override set and target Prefab asset without persisting it.",
                callSemantics: "Apply the selected Prefab instance property overrides to the explicit Prefab asset.",
                touchedContract: "Reports the explicit Prefab asset when the apply succeeds.",
                readPostconditionContract: "Prefab, asset lookup, and GUID lookup read surfaces covering the saved Prefab may be stale after apply.",
                failureSemantics: "Failure before apply leaves no requested Prefab asset mutation; Unity API failure may leave indeterminate Prefab asset state.",
                dangerousNotes: new[] { "This operation persists a Prefab asset from live instance state." }),
            exposure: UcliOperationExposure.EditLoweringOnly);

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryResolve(operation, args, executionContext, allowTemporaryState: true, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false)
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolve(operation, args, executionContext, allowTemporaryState: true, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: CreateTouched(state.TargetAssetPath)));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolve(operation, args, executionContext, allowTemporaryState: false, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var serializedObject = new SerializedObject(state.Component);
            serializedObject.UpdateIfRequiredOrScript();
            var appliedCount = 0;
            for (var i = 0; i < state.Changes.Count; i++)
            {
                var propertyPath = state.Changes[i].PropertyPath;
                var property = serializedObject.FindProperty(propertyPath);
                if (property == null)
                {
                    return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        $"SerializedProperty path was not found: {propertyPath}."));
                }

                try
                {
                    PrefabUtility.ApplyPropertyOverride(property, state.TargetAssetPath, InteractionMode.AutomatedAction);
                    appliedCount++;
                }
                catch (Exception exception)
                {
                    var operationFailure = new OperationFailure(
                        Code: UcliCoreErrorCodes.InternalError,
                        Message: $"Prefab override could not be applied: {propertyPath}. {exception.Message}",
                        OpId: operation.Id);
                    var partialResult = OperationPhaseStepResult.Failed(
                        operationFailure,
                        applied: appliedCount > 0,
                        changed: appliedCount > 0,
                        touched: appliedCount > 0 ? CreateTouched(state.TargetAssetPath) : null);
                    if (appliedCount > 0)
                    {
                        partialResult = partialResult
                            .WithPersistence()
                            .WithReadInvalidations(OperationReadInvalidationUtilities.CreateAssetSearchAndGuidPath());
                    }

                    return Task.FromResult(partialResult);
                }
            }

            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: CreateTouched(state.TargetAssetPath))
                .WithPersistence()
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateAssetSearchAndGuidPath()));
        }

        private static bool TryResolve (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out PrefabOverrideResolution.State state,
            out OperationPhaseStepResult? failure)
        {
            state = default;
            failure = null;
            if (!PrefabOverrideResolution.TryResolveForApply(operation, args, executionContext, allowTemporaryState, out state, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            return true;
        }

        private static IReadOnlyList<OperationTouch> CreateTouched (string prefabPath)
        {
            return new[]
            {
                OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Prefab, prefabPath)),
            };
        }

    }
}
