using System;
using MackySoft.Ucli.Contracts;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.prefab.revertOverrides</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class PrefabRevertOverridesOperation : UcliOperation<PrefabOverrideArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<PrefabOverrideArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.PrefabRevertOverrides,
            kind: UcliOperationKind.Mutation,
            description: "Reverts request-attributed Prefab instance property overrides on the live scene object.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.SceneContentMutation },
                touchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Scene },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate that selected property overrides came from this request and were not pre-existing.",
                callSemantics: "Revert selected live Prefab instance property overrides to the explicit Prefab asset value.",
                touchedContract: "Does not report touched persistence units because only live Play Mode scene state changes.",
                readPostconditionContract: "No persisted read surface is invalidated.",
                failureSemantics: "Failure before revert leaves no requested mutation; Unity API failure may leave live scene state partially changed.",
                dangerousNotes: new[] { "This operation mutates live scene objects but does not persist scene or Prefab assets." }),
            exposure: UcliOperationExposure.EditLoweringOnly);

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryResolve(operation, args, executionContext, out _, out var failure)
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
            return Task.FromResult(TryResolve(operation, args, executionContext, out _, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: true)
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolve(operation, args, executionContext, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var serializedObject = new SerializedObject(state.Component);
            serializedObject.UpdateIfRequiredOrScript();
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
                    PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                }
                catch (Exception exception)
                {
                    return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: UcliCoreErrorCodes.InternalError,
                        Message: $"Prefab override could not be reverted: {propertyPath}. {exception.Message}",
                        OpId: operation.Id)));
                }
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: true, changed: true));
        }

        private static bool TryResolve (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            out PrefabApplyOverridesOperation.ResolutionState state,
            out OperationPhaseStepResult? failure)
        {
            state = default;
            failure = null;
            if (!PrefabApplyOverridesOperation.TryResolveCommon(
                    operation,
                    args,
                    executionContext,
                    rejectPreRequestOverrides: true,
                    out state,
                    out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            return true;
        }
    }
}
