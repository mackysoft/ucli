using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.comp.set</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CompSetOperation : UcliOperation<ComponentSetArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<ComponentSetArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.CompSet,
            kind: UcliOperationKind.Mutation,
            description: "Assigns serialized property values on a component target.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.SceneContentMutation, UcliOperationSideEffect.PrefabContentMutation },
                touchedKinds: new[] { UcliTouchedResourceKind.Scene, UcliTouchedResourceKind.Prefab },
                planMode: UcliOperationPlanMode.MayCreatePreviewState,
                planSemantics: "Validate the component target and serialized property values, then compute preview changes without persisting project data.",
                callSemantics: "Apply serialized property values to the live component and leave saving to explicit save operations.",
                touchedContract: "Reports the scene or prefab resource dirtied by the component mutation when the target can be resolved.",
                readPostconditionContract: "Scene, prefab, component, and object read surfaces covering touched resources may be stale until refreshed.",
                failureSemantics: "Failure before apply leaves no requested mutation; failure during apply may leave live Unity state partially changed.",
                dangerousNotes: new[] { "This operation can dirty scene or prefab state without persisting it; callers must save or discard changes explicitly." }),
            exposure: UcliOperationExposure.EditLoweringOnly);

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            ComponentSetArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveValidateTarget(operation, args, executionContext, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            ComponentSetArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanBinding(operation, args, executionContext, out var bindingState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!ComponentOperationUtilities.TryCreateTemporaryComponentClone(bindingState.Binding.Component, executionContext, out var sandbox, out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: UcliCoreErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            var prefabOverrideStatesBeforeApply = CreatePrefabOverrideStateSnapshot(
                bindingState.Binding,
                bindingState.Sets,
                executionContext,
                allowExplicitPrefabAssetMutation: operation.AllowExplicitPrefabAssetMutation);
            if (!SerializedObjectValueApplier.TryApply(
                sandbox!,
                bindingState.Sets,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState,
                operation,
                out var changed,
                out var changedPropertyPaths,
                out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed)
            {
                executionContext.ReplaceTrackedTemporaryComponent(bindingState.Binding.Component, sandbox!, bindingState.Binding.Resource);

                executionContext.SetComponentShadow(
                    bindingState.Binding.SourceTrackingKey,
                    sandbox!,
                    bindingState.Binding.SourceComponent,
                    bindingState.Binding.OwnerGameObject,
                    bindingState.Binding.OwnerGameObjectTrackingKey,
                    bindingState.Binding.Resource);

                if (bindingState.Binding.Alias != null)
                {
                    executionContext.SetTemporaryAlias(bindingState.Binding.Alias, sandbox!, bindingState.Binding.Resource, bindingState.Binding.SourceTrackingKey);
                }

                executionContext.MarkRequestAttributedChange(bindingState.Binding.Resource);
                RecordPrefabOverridePropertyChanges(
                    operation.Id,
                    bindingState.Binding,
                    changedPropertyPaths,
                    prefabOverrideStatesBeforeApply,
                    sandbox!,
                    executionContext);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: changed,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(bindingState.Binding.Resource),
                }));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            ComponentSetArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveCallBinding(operation, args, executionContext, out var bindingState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!ComponentOperationUtilities.TryCreateTemporaryComponentClone(bindingState.Binding.Component, executionContext, out var sandbox, out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: UcliCoreErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            var prefabOverrideStatesBeforeApply = CreatePrefabOverrideStateSnapshot(
                bindingState.Binding,
                bindingState.Sets,
                executionContext,
                allowExplicitPrefabAssetMutation: operation.AllowExplicitPrefabAssetMutation);
            if (!SerializedObjectValueApplier.TryApply(
                sandbox!,
                bindingState.Sets,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryAliases,
                operation,
                out var changed,
                out var changedPropertyPaths,
                out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed
                && !SerializedObjectValueApplier.TryApply(
                    bindingState.Binding.Component,
                    bindingState.Sets,
                    executionContext,
                    OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryAliases,
                    operation,
                    out _,
                    out var commitErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: UcliCoreErrorCodes.InternalError,
                    Message: $"Validated component mutation could not be committed. {commitErrorMessage}",
                    OpId: operation.Id)));
            }

            if (changed)
            {
                executionContext.MarkRequestAttributedChange(bindingState.Binding.Resource);
                RecordPrefabOverridePropertyChanges(
                    operation.Id,
                    bindingState.Binding,
                    changedPropertyPaths,
                    prefabOverrideStatesBeforeApply,
                    bindingState.Binding.Component,
                    executionContext);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: changed,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(bindingState.Binding.Resource),
                }));
        }

        private static bool TryResolveValidateTarget (
            NormalizedOperation operation,
            ComponentSetArgs args,
            OperationExecutionContext executionContext,
            out ValidatedTargetState validatedTargetState,
            out OperationPhaseStepResult? failure)
        {
            validatedTargetState = default;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(
                    args,
                    operation.AliasReferences,
                    out var arguments,
                    out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                arguments.TargetReference,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState,
                out var componentResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validatedTargetState = new ValidatedTargetState(componentResolution.Component!, arguments);
            return true;
        }

        private static bool TryResolvePlanBinding (
            NormalizedOperation operation,
            ComponentSetArgs args,
            OperationExecutionContext executionContext,
            out ResolvedBindingState bindingState,
            out OperationPhaseStepResult? failure)
        {
            bindingState = default;
            failure = null;
            if (!TryResolveValidateTarget(operation, args, executionContext, out var validatedTargetState, out failure))
            {
                return false;
            }

            var targetReference = validatedTargetState.ParsedArguments.TargetReference;
            var alias = targetReference.Kind == UnityObjectReferenceKind.Alias
                ? targetReference.Alias
                : null;
            if (alias != null
                && executionContext.TryGetTemporaryAliasState(alias, out var temporaryAliasState))
            {
                var temporaryComponent = temporaryAliasState.UnityObject as Component;
                if (temporaryComponent == null)
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        "Reference did not resolve to a Component.");
                    return false;
                }

                var aliasSourceTrackingKey = temporaryAliasState.SourceTrackingKey
                    ?? executionContext.CreateComponentTrackingKey(temporaryComponent, temporaryAliasState.Resource);
                var aliasBinding = executionContext.TryGetComponentShadowState(aliasSourceTrackingKey, out var aliasComponentShadowState)
                    ? CreateShadowTargetBinding(
                        aliasComponentShadowState,
                        aliasSourceTrackingKey,
                        alias,
                        executionContext)
                    : CreateTargetBinding(
                        temporaryComponent,
                        temporaryAliasState.Resource,
                        aliasSourceTrackingKey,
                        alias,
                        executionContext);
                bindingState = new ResolvedBindingState(
                    aliasBinding,
                    validatedTargetState.ParsedArguments.Sets);
                return true;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                targetReference,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState,
                out var componentResolution,
                out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            var sourceTrackingKey = executionContext.CreateComponentTrackingKey(
                componentResolution.Component!,
                componentResolution.Resource);
            TargetBinding binding;
            if (executionContext.TryGetComponentShadowState(sourceTrackingKey, out var componentShadowState))
            {
                binding = CreateShadowTargetBinding(
                    componentShadowState,
                    sourceTrackingKey,
                    alias,
                    executionContext);
            }
            else
            {
                binding = CreateTargetBinding(
                    componentResolution.Component!,
                    componentResolution.Resource,
                    sourceTrackingKey,
                    alias,
                    executionContext);
            }

            bindingState = new ResolvedBindingState(binding, validatedTargetState.ParsedArguments.Sets);
            return true;
        }

        private static TargetBinding CreateShadowTargetBinding (
            ComponentSandboxRegistry.ComponentShadowState componentShadowState,
            RequestLocalObjectIdentity sourceTrackingKey,
            RequestLocalAliasIdentity? alias,
            OperationExecutionContext executionContext)
        {
            var component = componentShadowState.Component;
            var sourceComponent = componentShadowState.SourceComponent;
            if (component == null || sourceComponent == null)
            {
                throw new InvalidOperationException("A resolved component shadow must retain its live shadow and source components.");
            }

            return new TargetBinding(
                component,
                componentShadowState.Resource,
                sourceTrackingKey,
                sourceComponent,
                executionContext.CreateComponentTrackingKey(component, componentShadowState.Resource),
                ResolveComponentOwnerGameObject(component, executionContext),
                executionContext.CreateComponentOwnerTrackingKey(component, componentShadowState.Resource),
                alias);
        }

        private static bool TryResolveCallBinding (
            NormalizedOperation operation,
            ComponentSetArgs args,
            OperationExecutionContext executionContext,
            out ResolvedBindingState bindingState,
            out OperationPhaseStepResult? failure)
        {
            bindingState = default;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(
                    args,
                    operation.AliasReferences,
                    out var arguments,
                    out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                arguments.TargetReference,
                executionContext,
                OperationObjectReferenceUtilities.GetReferenceResolutionPolicy(operation, allowTemporaryState: false),
                out var componentResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            bindingState = new ResolvedBindingState(
                CreateTargetBinding(
                    componentResolution.Component!,
                    componentResolution.Resource,
                    executionContext.CreateComponentTrackingKey(
                        componentResolution.Component!,
                        componentResolution.Resource),
                    alias: null,
                    executionContext),
                arguments.Sets);
            return true;
        }

        private static TargetBinding CreateTargetBinding (
            Component component,
            OperationResource resource,
            RequestLocalObjectIdentity sourceTrackingKey,
            RequestLocalAliasIdentity? alias,
            OperationExecutionContext executionContext)
        {
            return new TargetBinding(
                component,
                resource,
                sourceTrackingKey,
                component,
                executionContext.CreateComponentTrackingKey(component, resource),
                ResolveComponentOwnerGameObject(component, executionContext),
                executionContext.CreateComponentOwnerTrackingKey(component, resource),
                alias);
        }

        private static GameObject ResolveComponentOwnerGameObject (
            Component component,
            OperationExecutionContext executionContext)
        {
            if (executionContext.TryResolveTrackedComponentOwnerGameObject(component, out var ownerGameObject)
                && ownerGameObject != null)
            {
                return ownerGameObject;
            }

            return component.gameObject;
        }

        private static void RecordPrefabOverridePropertyChanges (
            IpcExecuteStepId editStepId,
            TargetBinding binding,
            IReadOnlyList<string> changedPropertyPaths,
            IReadOnlyDictionary<string, PrefabOverridePropertyStateSnapshot>? prefabOverrideStatesBeforeApply,
            Component finalComponent,
            OperationExecutionContext executionContext)
        {
            if (binding.Resource.Kind != UcliTouchedResourceKind.Scene
                || changedPropertyPaths.Count == 0
                || finalComponent == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(finalComponent);
            serializedObject.UpdateIfRequiredOrScript();
            for (var i = 0; i < changedPropertyPaths.Count; i++)
            {
                var property = serializedObject.FindProperty(changedPropertyPaths[i]);
                if (property == null)
                {
                    continue;
                }

                if (!TryResolvePropertyStateBeforeRequest(
                        editStepId,
                        binding.PrefabOverrideTargetKey,
                        changedPropertyPaths[i],
                        prefabOverrideStatesBeforeApply,
                        executionContext,
                        out var stateBeforeApply))
                {
                    continue;
                }

                executionContext.RecordPrefabOverridePropertyChange(
                    editStepId,
                    binding.PrefabOverrideTargetKey,
                    changedPropertyPaths[i],
                    stateBeforeApply.WasPrefabOverrideBeforeRequest,
                    stateBeforeApply.ValueHash,
                    SerializedPropertyValueHasher.Create(property, executionContext, binding.Resource),
                    stateBeforeApply.RequiresExplicitPrefabAssetMutation);
            }
        }

        private static bool TryResolvePropertyStateBeforeRequest (
            IpcExecuteStepId editStepId,
            RequestLocalObjectIdentity targetKey,
            string propertyPath,
            IReadOnlyDictionary<string, PrefabOverridePropertyStateSnapshot>? statesBeforeApply,
            OperationExecutionContext executionContext,
            out PrefabOverridePropertyStateSnapshot stateBeforeRequest)
        {
            stateBeforeRequest = default;
            if (statesBeforeApply != null
                && statesBeforeApply.TryGetValue(propertyPath, out stateBeforeRequest))
            {
                return true;
            }

            if (!executionContext.TryGetPrefabOverridePropertyChange(editStepId, targetKey, propertyPath, out var existingChange))
            {
                return false;
            }

            stateBeforeRequest = new PrefabOverridePropertyStateSnapshot(
                existingChange.WasPrefabOverrideBeforeRequest,
                existingChange.ValueHashBeforeRequest,
                existingChange.RequiresExplicitPrefabAssetMutation);
            return true;
        }

        private static IReadOnlyDictionary<string, PrefabOverridePropertyStateSnapshot>? CreatePrefabOverrideStateSnapshot (
            TargetBinding binding,
            IReadOnlyList<SerializedPropertyAssignment> assignments,
            OperationExecutionContext executionContext,
            bool allowExplicitPrefabAssetMutation)
        {
            if (binding.Resource.Kind != UcliTouchedResourceKind.Scene
                || !TryResolvePrefabOverrideStateComponent(
                    binding,
                    executionContext,
                    allowExplicitPrefabAssetMutation,
                    out var stateComponent,
                    out var isPrefabInstance,
                    out var requiresExplicitPrefabAssetMutation))
            {
                return null;
            }

            var statesByPath = new Dictionary<string, PrefabOverridePropertyStateSnapshot>(assignments.Count, StringComparer.Ordinal);
            var serializedObject = new SerializedObject(stateComponent!);
            serializedObject.UpdateIfRequiredOrScript();
            for (var i = 0; i < assignments.Count; i++)
            {
                var propertyPath = assignments[i].Path.Value;
                if (statesByPath.ContainsKey(propertyPath))
                {
                    continue;
                }

                var property = serializedObject.FindProperty(propertyPath);
                if (property == null)
                {
                    continue;
                }

                statesByPath.Add(
                    propertyPath,
                    new PrefabOverridePropertyStateSnapshot(
                        isPrefabInstance && property.prefabOverride,
                        SerializedPropertyValueHasher.Create(property, executionContext, binding.Resource),
                        requiresExplicitPrefabAssetMutation));
            }

            return statesByPath;
        }

        private static bool TryResolvePrefabOverrideStateComponent (
            TargetBinding binding,
            OperationExecutionContext executionContext,
            bool allowExplicitPrefabAssetMutation,
            out Component? stateComponent,
            out bool isPrefabInstance,
            out bool requiresExplicitPrefabAssetMutation)
        {
            stateComponent = binding.Component;
            isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(binding.Component);
            requiresExplicitPrefabAssetMutation = false;
            if (isPrefabInstance)
            {
                return true;
            }

            if (executionContext.TryResolveTemporarySceneSourceObject(binding.Resource.Path, binding.Component, out var sourceObject))
            {
                var sourceComponent = sourceObject as Component;
                if (sourceComponent != null
                    && PrefabUtility.IsPartOfPrefabInstance(sourceComponent))
                {
                    stateComponent = sourceComponent;
                    isPrefabInstance = true;
                    return true;
                }
            }

            if (binding.SourceComponent != binding.Component
                && PrefabUtility.IsPartOfPrefabInstance(binding.SourceComponent))
            {
                stateComponent = binding.SourceComponent;
                isPrefabInstance = true;
                return true;
            }

            if (executionContext.IsPlannedPrefabInstanceLineage(binding.Component))
            {
                requiresExplicitPrefabAssetMutation = allowExplicitPrefabAssetMutation;
                return true;
            }

            if (allowExplicitPrefabAssetMutation
                && EditorApplication.isPlaying)
            {
                // NOTE: Unity can expose Play Mode scene objects without normal PrefabUtility instance
                // linkage. The explicit Prefab override action validates the requested asset path before
                // copying values, so record this set as an explicit-asset candidate instead of losing it.
                requiresExplicitPrefabAssetMutation = true;
                return true;
            }

            stateComponent = null;
            return false;
        }

        private readonly struct TargetBinding
        {
            public TargetBinding (
                Component component,
                OperationResource resource,
                RequestLocalObjectIdentity sourceTrackingKey,
                Component sourceComponent,
                RequestLocalObjectIdentity prefabOverrideTargetKey,
                GameObject ownerGameObject,
                RequestLocalObjectIdentity ownerGameObjectTrackingKey,
                RequestLocalAliasIdentity? alias)
            {
                Component = component;
                Resource = resource;
                SourceTrackingKey = sourceTrackingKey;
                SourceComponent = sourceComponent;
                PrefabOverrideTargetKey = prefabOverrideTargetKey;
                OwnerGameObject = ownerGameObject;
                OwnerGameObjectTrackingKey = ownerGameObjectTrackingKey;
                Alias = alias;
            }

            public Component Component { get; }

            public OperationResource Resource { get; }

            public RequestLocalObjectIdentity SourceTrackingKey { get; }

            public Component SourceComponent { get; }

            public RequestLocalObjectIdentity PrefabOverrideTargetKey { get; }

            public GameObject OwnerGameObject { get; }

            public RequestLocalObjectIdentity OwnerGameObjectTrackingKey { get; }

            public RequestLocalAliasIdentity? Alias { get; }
        }

        private readonly struct PrefabOverridePropertyStateSnapshot
        {
            public PrefabOverridePropertyStateSnapshot (
                bool wasPrefabOverrideBeforeRequest,
                string valueHash,
                bool requiresExplicitPrefabAssetMutation)
            {
                WasPrefabOverrideBeforeRequest = wasPrefabOverrideBeforeRequest;
                ValueHash = valueHash;
                RequiresExplicitPrefabAssetMutation = requiresExplicitPrefabAssetMutation;
            }

            public bool WasPrefabOverrideBeforeRequest { get; }

            public string ValueHash { get; }

            public bool RequiresExplicitPrefabAssetMutation { get; }
        }

        private readonly struct ValidatedTargetState
        {
            public ValidatedTargetState (
                Component component,
                SerializedObjectSetArguments parsedArguments)
            {
                Component = component;
                ParsedArguments = parsedArguments;
            }

            public Component? Component { get; }

            public SerializedObjectSetArguments ParsedArguments { get; }
        }

        private readonly struct ResolvedBindingState
        {
            public ResolvedBindingState (
                TargetBinding binding,
                IReadOnlyList<SerializedPropertyAssignment> sets)
            {
                Binding = binding;
                Sets = sets;
            }

            public TargetBinding Binding { get; }

            public IReadOnlyList<SerializedPropertyAssignment> Sets { get; }
        }
    }
}
