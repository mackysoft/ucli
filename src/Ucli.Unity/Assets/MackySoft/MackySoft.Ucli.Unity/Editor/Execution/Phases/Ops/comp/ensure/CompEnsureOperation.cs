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
    /// <summary> Implements <c>ucli.comp.ensure</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CompEnsureOperation : IUcliOperation
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
                    ""hierarchyPath"": { ""type"": ""string"", ""minLength"": 1 }
                  },
                  ""oneOf"": [
                    { ""required"": [""var""] },
                    { ""required"": [""globalObjectId""] },
                    { ""required"": [""scene"", ""hierarchyPath""] },
                    { ""required"": [""prefab"", ""hierarchyPath""] }
                  ]
                },
                ""type"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""target"", ""type""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.CompEnsure,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, executionContext, allowTemporaryState: true, out _, out var failure))
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
            return Execute(operation, executionContext, applied: false);
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Execute(operation, executionContext, applied: true);
        }

        private static Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied)
        {
            if (!TryValidateArguments(
                operation,
                executionContext,
                allowTemporaryState: !applied,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var component = default(Component);
            var changed = false;
            if (applied)
            {
                var existingComponents = validationState.Target.GetComponents(validationState.ComponentType);
                component = existingComponents.Length > 0
                    ? existingComponents[0]
                    : null;
                changed = component == null;
            }
            else
            {
                var targetReferenceKey = UnityObjectReferenceResolver.CreateTrackingKey(validationState.Target);
                if (executionContext.TryGetEnsuredComponentState(targetReferenceKey, validationState.ComponentType, out var ensuredComponentState))
                {
                    component = ensuredComponentState.Component;
                    changed = false;
                }
                else
                {
                    var existingComponents = validationState.Target.GetComponents(validationState.ComponentType);
                    component = existingComponents.Length > 0
                        ? existingComponents[0]
                        : null;
                    changed = component == null;
                    if (component == null)
                    {
                        if (!ComponentOperationUtilities.TryCreateTemporaryComponent(validationState.ComponentType, executionContext, out component, out var errorMessage))
                        {
                            return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                                Code: MackySoft.Ucli.Contracts.Ipc.IpcErrorCodes.InternalError,
                                Message: errorMessage,
                                OpId: operation.Id)));
                        }

                        executionContext.SetEnsuredComponent(targetReferenceKey, validationState.ComponentType, component!, validationState.Resource);
                    }
                }
            }

            if (!applied && operation.As != null)
            {
                if (component != null)
                {
                    executionContext.SetTemporaryAlias(operation.As, component, validationState.Resource, UnityObjectReferenceResolver.CreateTrackingKey(component));
                }
                else
                {
                    throw new InvalidOperationException("Component planning state could not be resolved.");
                }
            }

            if (applied)
            {
                if (component == null)
                {
                    component = AddComponent(validationState.Target, validationState.ComponentType);
                }

                if (operation.As != null)
                {
                    StoreAliasIfNeeded(operation.As, executionContext, component!, validationState.Resource);
                }
            }

            if (changed)
            {
                executionContext.MarkRequestAttributedChange(validationState.Resource);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: changed,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(validationState.Resource),
                }));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!CompEnsureArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                parsedArguments.TargetReference,
                executionContext,
                allowTemporaryState,
                out var targetResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentTypeResolver.TryResolveComponentType(parsedArguments.TypeId, out var resolvedComponentType, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validationState = new ValidationState(
                targetResolution.GameObject!,
                targetResolution.Resource,
                resolvedComponentType!);
            return true;
        }

        private static Component AddComponent (
            GameObject target,
            Type componentType)
        {
            var existingTransform = target.transform;
            if (componentType == typeof(Transform))
            {
                return existingTransform;
            }

            var component = target.AddComponent(componentType);
            if (component == null)
            {
                throw new InvalidOperationException($"Component could not be added. type={componentType.FullName}");
            }

            return component;
        }

        private static void StoreAliasIfNeeded (
            string? alias,
            OperationExecutionContext executionContext,
            Component component,
            OperationResource resource)
        {
            if (alias == null)
            {
                return;
            }

            executionContext.SetTemporaryAlias(alias, component, resource, UnityObjectReferenceResolver.CreateTrackingKey(component));
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(component, out var resolvedReference))
            {
                executionContext.AliasStore.Set(alias, resolvedReference!);
            }
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                OperationResource resource,
                Type componentType)
            {
                Target = target;
                Resource = resource;
                ComponentType = componentType;
            }

            public GameObject? Target { get; }

            public OperationResource Resource { get; }

            public Type? ComponentType { get; }
        }
    }
}
