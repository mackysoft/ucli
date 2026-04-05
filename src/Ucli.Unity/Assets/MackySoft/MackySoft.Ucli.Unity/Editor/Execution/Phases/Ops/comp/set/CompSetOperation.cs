using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.comp.set</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CompSetOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""target"": {
                  ""type"": ""object"",
                  ""additionalProperties"": false,
                  ""properties"": {
                    ""var"": { ""type"": ""string"", ""minLength"": 1 },
                    ""globalObjectId"": { ""type"": ""string"", ""minLength"": 1 },
                    ""scene"": { ""type"": ""string"", ""minLength"": 1 },
                    ""prefab"": { ""type"": ""string"", ""minLength"": 1 },
                    ""hierarchyPath"": { ""type"": ""string"", ""minLength"": 1 },
                    ""componentType"": { ""type"": ""string"", ""minLength"": 1 }
                  },
                  ""oneOf"": [
                    { ""required"": [""var""] },
                    { ""required"": [""globalObjectId""] },
                    { ""required"": [""scene"", ""hierarchyPath"", ""componentType""] },
                    { ""required"": [""prefab"", ""hierarchyPath"", ""componentType""] }
                  ]
                },
                ""sets"": {
                  ""type"": ""array"",
                  ""minItems"": 1,
                  ""items"": {
                    ""type"": ""object"",
                    ""additionalProperties"": false,
                    ""properties"": {
                      ""path"": { ""type"": ""string"", ""minLength"": 1 },
                      ""value"": {}
                    },
                    ""required"": [""path"", ""value""]
                  }
                }
              },
              ""required"": [""target"", ""sets""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.CompSet,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveValidateTarget(operation, executionContext, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanBinding(operation, executionContext, out var bindingState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!ComponentOperationUtilities.TryCreateTemporaryComponentClone(bindingState.Binding.Component, executionContext, out var sandbox, out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            if (!SerializedObjectValueApplier.TryApply(
                sandbox!,
                bindingState.Sets,
                executionContext,
                GetPlanReferenceResolutionPolicy(operation),
                out var changed,
                out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed)
            {
                executionContext.ReplaceTrackedTemporaryComponent(bindingState.Binding.Component, sandbox!, bindingState.Binding.Resource);

                if (bindingState.Binding.SourceGlobalObjectId != null)
                {
                    executionContext.SetComponentShadow(bindingState.Binding.SourceGlobalObjectId, sandbox!, bindingState.Binding.Resource);
                }

                if (bindingState.Binding.Alias != null)
                {
                    executionContext.SetTemporaryAlias(bindingState.Binding.Alias, sandbox!, bindingState.Binding.Resource, bindingState.Binding.SourceGlobalObjectId);
                }

                executionContext.MarkRequestAttributedChange(bindingState.Binding.Resource);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: changed,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(bindingState.Binding.Resource),
                }));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveCallBinding(operation, executionContext, out var bindingState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!ComponentOperationUtilities.TryCreateTemporaryComponentClone(bindingState.Binding.Component, executionContext, out var sandbox, out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            if (!SerializedObjectValueApplier.TryApply(
                sandbox!,
                bindingState.Sets,
                executionContext,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryAliases,
                out var changed,
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
                    out _,
                    out var commitErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Validated component mutation could not be committed. {commitErrorMessage}",
                    OpId: operation.Id)));
            }

            if (changed)
            {
                executionContext.MarkRequestAttributedChange(bindingState.Binding.Resource);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: changed,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(bindingState.Binding.Resource),
                }));
        }

        private static OperationObjectReferenceUtilities.ReferenceResolutionPolicy GetPlanReferenceResolutionPolicy (
            NormalizedOperation operation)
        {
            return string.IsNullOrWhiteSpace(operation.InternalExecutionKey)
                ? OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryAliases
                : OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState;
        }

        private static bool TryResolveValidateTarget (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out ValidatedTargetState validatedTargetState,
            out OperationPhaseStepResult? failure)
        {
            validatedTargetState = default;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                arguments.TargetReference,
                executionContext,
                allowTemporaryState: true,
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
            OperationExecutionContext executionContext,
            out ResolvedBindingState bindingState,
            out OperationPhaseStepResult? failure)
        {
            bindingState = default;
            failure = null;
            if (!TryResolveValidateTarget(operation, executionContext, out var validatedTargetState, out failure))
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

                bindingState = new ResolvedBindingState(
                    new TargetBinding(
                        temporaryComponent,
                        temporaryAliasState.Resource,
                        temporaryAliasState.SourceGlobalObjectId,
                        alias),
                    validatedTargetState.ParsedArguments.Sets);
                return true;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                targetReference,
                executionContext,
                allowTemporaryState: true,
                out var componentResolution,
                out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            var sourceGlobalObjectId = GetSourceReferenceKey(targetReference, componentResolution.Component!);
            TargetBinding binding;
            if (!string.IsNullOrWhiteSpace(sourceGlobalObjectId)
                && executionContext.TryGetComponentShadowState(sourceGlobalObjectId, out var componentShadowState))
            {
                binding = new TargetBinding(
                    componentShadowState.Component!,
                    componentShadowState.Resource,
                    sourceGlobalObjectId,
                    alias);
            }
            else
            {
                binding = new TargetBinding(componentResolution.Component!, componentResolution.Resource, sourceGlobalObjectId, alias);
            }

            bindingState = new ResolvedBindingState(binding, validatedTargetState.ParsedArguments.Sets);
            return true;
        }

        private static bool TryResolveCallBinding (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out ResolvedBindingState bindingState,
            out OperationPhaseStepResult? failure)
        {
            bindingState = default;
            failure = null;
            if (!SerializedObjectSetArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                arguments.TargetReference,
                executionContext,
                allowTemporaryState: false,
                out var componentResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            bindingState = new ResolvedBindingState(
                new TargetBinding(
                    componentResolution.Component!,
                    componentResolution.Resource,
                    GetSourceReferenceKey(arguments.TargetReference, componentResolution.Component!),
                    alias: null),
                arguments.Sets);
            return true;
        }

        private static string GetSourceReferenceKey (
            UnityObjectReference targetReference,
            Component component)
        {
            if (targetReference.Kind == UnityObjectReferenceKind.Selector
                && targetReference.Selector.Kind == ResolveSelectorKind.GlobalObjectId)
            {
                return targetReference.Selector.GlobalObjectId!;
            }

            return UnityObjectReferenceResolver.CreateTrackingKey(component);
        }

        private readonly struct TargetBinding
        {
            public TargetBinding (
                Component component,
                OperationResource resource,
                string? sourceGlobalObjectId,
                string? alias)
            {
                Component = component;
                Resource = resource;
                SourceGlobalObjectId = sourceGlobalObjectId;
                Alias = alias;
            }

            public Component Component { get; }

            public OperationResource Resource { get; }

            public string? SourceGlobalObjectId { get; }

            public string? Alias { get; }
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
                System.Collections.Generic.IReadOnlyList<SerializedPropertyAssignment> sets)
            {
                Binding = binding;
                Sets = sets;
            }

            public TargetBinding Binding { get; }

            public System.Collections.Generic.IReadOnlyList<SerializedPropertyAssignment> Sets { get; }
        }
    }
}
