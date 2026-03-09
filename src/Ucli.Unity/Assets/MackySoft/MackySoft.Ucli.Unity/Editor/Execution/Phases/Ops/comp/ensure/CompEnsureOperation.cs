using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                    ""hierarchyPath"": { ""type"": ""string"", ""minLength"": 1 }
                  },
                  ""oneOf"": [
                    { ""required"": [""var""] },
                    { ""required"": [""globalObjectId""] },
                    { ""required"": [""scene"", ""hierarchyPath""] }
                  ]
                },
                ""type"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""target"", ""type""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.comp.ensure",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, executionContext, out _, out _, out _, out var failure))
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
            if (!TryValidateArguments(operation, executionContext, out var target, out var scene, out var componentType, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var component = default(Component);
            var changed = false;
            var usesTemporaryComponent = false;
            if (applied)
            {
                var existingComponents = target.GetComponents(componentType);
                component = existingComponents.Length > 0
                    ? existingComponents[0]
                    : null;
                changed = component == null;
            }
            else
            {
                var targetGlobalObjectId = UnityObjectReferenceResolver.CreateResolvedReference(target).GlobalObjectId;
                if (!string.IsNullOrWhiteSpace(targetGlobalObjectId)
                    && executionContext.TryGetEnsuredComponent(targetGlobalObjectId, componentType, out component, out _))
                {
                    changed = false;
                    usesTemporaryComponent = true;
                }
                else
                {
                    var existingComponents = target.GetComponents(componentType);
                    component = existingComponents.Length > 0
                        ? existingComponents[0]
                        : null;
                    changed = component == null;
                    if (component == null)
                    {
                        if (!ComponentOperationUtilities.TryCreateTemporaryComponent(componentType, executionContext, out component, out var errorMessage))
                        {
                            return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                                Code: MackySoft.Ucli.Contracts.Ipc.IpcErrorCodes.InternalError,
                                Message: errorMessage,
                                OpId: operation.Id)));
                        }

                        executionContext.SetEnsuredComponent(targetGlobalObjectId, componentType, component!, scene.path);
                        usesTemporaryComponent = true;
                    }
                }
            }

            if (!applied && operation.As != null)
            {
                if (component != null)
                {
                    if (usesTemporaryComponent)
                    {
                        executionContext.SetTemporaryAlias(operation.As, component, scene.path);
                    }
                    else
                    {
                        executionContext.AliasStore.Set(operation.As, UnityObjectReferenceResolver.CreateResolvedReference(component));
                    }
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
                    component = AddComponent(target, componentType);
                }

                if (operation.As != null)
                {
                    executionContext.AliasStore.Set(operation.As, UnityObjectReferenceResolver.CreateResolvedReference(component!));
                }
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: changed,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scene.path),
                }));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out GameObject target,
            out Scene scene,
            out Type componentType,
            out OperationPhaseStepResult? failure)
        {
            target = null!;
            scene = default;
            componentType = null!;
            failure = null;
            if (!CompEnsureArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!ComponentOperationUtilities.TryResolveLoadedSceneGameObject(
                parsedArguments.TargetReference,
                executionContext,
                out var resolvedTarget,
                out scene,
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

            target = resolvedTarget!;
            componentType = resolvedComponentType!;
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
    }
}