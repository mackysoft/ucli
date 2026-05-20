using System;
using System.Collections.Generic;
using System.Linq;
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
                sideEffects: new[] { UcliOperationSideEffect.PrefabSave },
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
            if (!TryResolve(operation, args, executionContext, out var state, out var failure))
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
                    PrefabUtility.ApplyPropertyOverride(property, state.TargetAssetPath, InteractionMode.AutomatedAction);
                }
                catch (Exception exception)
                {
                    return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: UcliCoreErrorCodes.InternalError,
                        Message: $"Prefab override could not be applied: {propertyPath}. {exception.Message}",
                        OpId: operation.Id)));
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
            out ResolutionState state,
            out OperationPhaseStepResult? failure)
        {
            state = default;
            failure = null;
            if (!TryResolveCommon(operation, args, executionContext, rejectPreRequestOverrides: false, out state, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            return true;
        }

        internal static bool TryResolveCommon (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            bool rejectPreRequestOverrides,
            out ResolutionState state,
            out string errorMessage)
        {
            state = default;
            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var targetReference, out errorMessage))
            {
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                targetReference,
                executionContext,
                allowTemporaryState: true,
                out var componentResolution,
                out errorMessage))
            {
                return false;
            }

            var component = componentResolution.Component!;
            if (componentResolution.Resource.Kind != OperationTouchKind.Scene)
            {
                errorMessage = "Prefab override actions require a scene component target.";
                return false;
            }

            var targetAssetPath = args.TargetAssetPath.Value;
            if (!PrefabOperationUtilities.TryEnsurePrefabAssetExists(targetAssetPath, out errorMessage))
            {
                return false;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(component))
            {
                errorMessage = "Prefab override actions require a Prefab instance component target.";
                return false;
            }

            if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, targetAssetPath) == null)
            {
                errorMessage = $"Prefab override target asset is not in the target instance lineage: {targetAssetPath}.";
                return false;
            }

            var targetKey = UnityObjectReferenceResolver.CreateTrackingKey(component);
            var requestedPropertyPaths = args.PropertyPaths?.Select(static path => path.Value).ToArray();
            if (!executionContext.TryCollectPrefabOverridePropertyChanges(
                    targetKey,
                    requestedPropertyPaths,
                    out var changes,
                    out errorMessage))
            {
                return false;
            }

            if (rejectPreRequestOverrides)
            {
                for (var i = 0; i < changes.Count; i++)
                {
                    if (changes[i].WasPrefabOverrideBeforeRequest)
                    {
                        errorMessage = $"Prefab override property already existed before the request: {changes[i].PropertyPath}.";
                        return false;
                    }
                }
            }

            if (!TryValidateProperties(component, changes, out errorMessage))
            {
                return false;
            }

            state = new ResolutionState(component, targetAssetPath, changes);
            return true;
        }

        internal static bool TryValidateProperties (
            Component component,
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
            out string errorMessage)
        {
            var serializedObject = new SerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();
            for (var i = 0; i < changes.Count; i++)
            {
                if (serializedObject.FindProperty(changes[i].PropertyPath) == null)
                {
                    errorMessage = $"SerializedProperty path was not found: {changes[i].PropertyPath}.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static IReadOnlyList<OperationTouch> CreateTouched (string prefabPath)
        {
            return new[]
            {
                OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Prefab, prefabPath)),
            };
        }

        internal readonly struct ResolutionState
        {
            public ResolutionState (
                Component component,
                string targetAssetPath,
                IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes)
            {
                Component = component;
                TargetAssetPath = targetAssetPath;
                Changes = changes;
            }

            public Component Component { get; }

            public string TargetAssetPath { get; }

            public IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> Changes { get; }
        }
    }
}
