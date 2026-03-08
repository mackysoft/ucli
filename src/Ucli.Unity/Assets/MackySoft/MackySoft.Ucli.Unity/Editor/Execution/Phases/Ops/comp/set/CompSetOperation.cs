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
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.comp.set",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveValidateTarget(operation, executionContext, out _, out _, out var failure))
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
            if (!TryResolvePlanBinding(operation, executionContext, out var binding, out var sets, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!ComponentOperationUtilities.TryCreateTemporaryComponentClone(binding.Component, executionContext, out var sandbox, out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            if (!CompSetValueApplier.TryApply(sandbox!, sets!, executionContext, allowTemporaryState: true, out var changed, out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed)
            {
                if (binding.SourceGlobalObjectId != null)
                {
                    executionContext.SetComponentShadow(binding.SourceGlobalObjectId, sandbox!, binding.ScenePath);
                }

                if (binding.Alias != null)
                {
                    executionContext.SetTemporaryAlias(binding.Alias, sandbox!, binding.ScenePath);
                }
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: changed,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(binding.ScenePath),
                }));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveCallBinding(operation, executionContext, out var binding, out var sets, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!ComponentOperationUtilities.TryCreateTemporaryComponentClone(binding.Component, executionContext, out var sandbox, out var cloneErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: cloneErrorMessage,
                    OpId: operation.Id)));
            }

            if (!CompSetValueApplier.TryApply(sandbox!, sets!, executionContext, allowTemporaryState: false, out var changed, out var applyErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, applyErrorMessage));
            }

            if (changed
                && !CompSetValueApplier.TryApply(binding.Component, sets!, executionContext, allowTemporaryState: false, out _, out var commitErrorMessage))
            {
                return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Validated component mutation could not be committed. {commitErrorMessage}",
                    OpId: operation.Id)));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: changed,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(binding.ScenePath),
                }));
        }

        private static bool TryResolveValidateTarget (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out Component? component,
            out CompSetArguments? parsedArguments,
            out OperationPhaseStepResult? failure)
        {
            component = null;
            parsedArguments = null;
            failure = null;
            if (!CompSetArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                arguments.TargetReference,
                executionContext,
                allowTemporaryState: true,
                out component,
                out _,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            parsedArguments = arguments;
            return true;
        }

        private static bool TryResolvePlanBinding (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out TargetBinding binding,
            out System.Collections.Generic.IReadOnlyList<CompSetAssignment>? sets,
            out OperationPhaseStepResult? failure)
        {
            binding = default;
            sets = null;
            failure = null;
            if (!TryResolveValidateTarget(operation, executionContext, out _, out var parsedArguments, out failure))
            {
                return false;
            }

            var targetReference = parsedArguments!.Value.TargetReference;
            var alias = targetReference.Kind == UnityObjectReferenceKind.Alias
                ? targetReference.Alias
                : null;
            if (alias != null
                && executionContext.TryGetTemporaryAlias(alias, out var temporaryObject, out var temporaryScenePath))
            {
                var temporaryComponent = temporaryObject as Component;
                if (temporaryComponent == null)
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        "Reference did not resolve to a Component.");
                    return false;
                }

                binding = new TargetBinding(temporaryComponent, temporaryScenePath, sourceGlobalObjectId: null, alias);
                sets = parsedArguments.Value.Sets;
                return true;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                targetReference,
                executionContext,
                allowTemporaryState: false,
                out var resolvedComponent,
                out var scenePath,
                out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            var sourceGlobalObjectId = GetSourceGlobalObjectId(targetReference, resolvedComponent!);
            if (!string.IsNullOrWhiteSpace(sourceGlobalObjectId)
                && executionContext.TryGetComponentShadow(sourceGlobalObjectId, out var shadowComponent, out var shadowScenePath))
            {
                binding = new TargetBinding(shadowComponent!, shadowScenePath, sourceGlobalObjectId, alias);
            }
            else
            {
                binding = new TargetBinding(resolvedComponent!, scenePath, sourceGlobalObjectId, alias);
            }

            sets = parsedArguments.Value.Sets;
            return true;
        }

        private static bool TryResolveCallBinding (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out TargetBinding binding,
            out System.Collections.Generic.IReadOnlyList<CompSetAssignment>? sets,
            out OperationPhaseStepResult? failure)
        {
            binding = default;
            sets = null;
            failure = null;
            if (!CompSetArgumentsCodec.TryParse(operation.Args, out var arguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveComponent(
                arguments.TargetReference,
                executionContext,
                allowTemporaryState: false,
                out var component,
                out var scenePath,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            binding = new TargetBinding(component!, scenePath, GetSourceGlobalObjectId(arguments.TargetReference, component!), alias: null);
            sets = arguments.Sets;
            return true;
        }

        private static string GetSourceGlobalObjectId (
            UnityObjectReference targetReference,
            Component component)
        {
            if (targetReference.Kind == UnityObjectReferenceKind.Selector
                && targetReference.Selector.Kind == ResolveSelectorKind.GlobalObjectId)
            {
                return targetReference.Selector.GlobalObjectId!;
            }

            return UnityObjectReferenceResolver.CreateResolvedReference(component).GlobalObjectId;
        }

        private readonly struct TargetBinding
        {
            public TargetBinding (
                Component component,
                string scenePath,
                string? sourceGlobalObjectId,
                string? alias)
            {
                Component = component;
                ScenePath = scenePath;
                SourceGlobalObjectId = sourceGlobalObjectId;
                Alias = alias;
            }

            public Component Component { get; }

            public string ScenePath { get; }

            public string? SourceGlobalObjectId { get; }

            public string? Alias { get; }
        }
    }
}
