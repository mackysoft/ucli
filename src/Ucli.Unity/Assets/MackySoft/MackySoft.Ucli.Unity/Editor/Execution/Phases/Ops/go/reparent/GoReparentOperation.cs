using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.go.reparent</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoReparentOperation : IUcliOperation
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
                ""parent"": {
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
                }
              },
              ""required"": [""target"", ""parent""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.GoReparent,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, executionContext, allowTemporaryState: true, out _, out var failure))
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
            if (!TryValidate(operation, executionContext, allowTemporaryState: true, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsurePlanResourceState(
                    state.TargetResource,
                    executionContext,
                    out var targetPreparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    targetPreparationErrorMessage));
            }

            if (!AreSameResource(state.TargetResource, state.ParentResource)
                && !GoOperationUtilities.TryEnsurePlanResourceState(
                    state.ParentResource,
                    executionContext,
                    out var parentPreparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    parentPreparationErrorMessage));
            }

            if (!TryValidate(operation, executionContext, allowTemporaryState: true, out state, out failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                state.Target,
                state.TargetResource,
                executionContext,
                out var targetErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, targetErrorMessage));
            }

            if (!GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                state.Parent,
                state.ParentResource,
                executionContext,
                out var parentErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parentErrorMessage));
            }

            if (state.Target.transform.parent == state.Parent.transform)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false));
            }

            state.Target.transform.SetParent(state.Parent.transform, worldPositionStays: false);
            GoOperationUtilities.MarkPlanResourceDirty(state.TargetResource, executionContext);
            executionContext.MarkRequestAttributedChange(state.TargetResource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: CreateTouched(state)));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, executionContext, allowTemporaryState: false, out var state, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (state.Target.transform.parent == state.Parent.transform)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: true,
                    changed: false));
            }

            state.Target.transform.SetParent(state.Parent.transform, worldPositionStays: false);
            executionContext.MarkRequestAttributedChange(state.TargetResource);
            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: CreateTouched(state))
                .WithReadInvalidations(CreateReadInvalidations(state.TargetResource)));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState state,
            out OperationPhaseStepResult? failure)
        {
            state = default;
            failure = null;
            if (!TryParseReference(operation.Args, GoOperationPropertyNames.Target, out var targetReference, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!TryParseReference(operation.Args, GoOperationPropertyNames.Parent, out var parentReference, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                targetReference,
                executionContext,
                allowTemporaryState,
                out var targetResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                parentReference,
                executionContext,
                allowTemporaryState,
                out var parentResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!AreSameResource(targetResolution.Resource, parentResolution.Resource))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, "Reparent target and parent must belong to the same editable context.");
                return false;
            }

            if (targetResolution.GameObject == parentResolution.GameObject
                || parentResolution.GameObject!.transform.IsChildOf(targetResolution.GameObject!.transform))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, "Reparent would create a transform cycle.");
                return false;
            }

            state = new ValidationState(
                targetResolution.GameObject!,
                parentResolution.GameObject!,
                targetResolution.Resource,
                parentResolution.Resource);
            return true;
        }

        private static bool TryParseReference (
            JsonElement args,
            string propertyName,
            out UnityObjectReference reference,
            out string errorMessage)
        {
            reference = default;
            errorMessage = string.Empty;
            if (!args.TryGetProperty(propertyName, out var referenceElement))
            {
                errorMessage = $"Operation 'args' requires property '{propertyName}'.";
                return false;
            }

            return UnityObjectReferenceCodec.TryParse(referenceElement, $"args.{propertyName}", out reference, out errorMessage);
        }

        private static bool AreSameResource (
            OperationResource left,
            OperationResource right)
        {
            return left.Kind == right.Kind && string.Equals(left.Path, right.Path, System.StringComparison.Ordinal);
        }

        private static OperationTouch[] CreateTouched (in ValidationState state)
        {
            if (AreSameResource(state.TargetResource, state.ParentResource))
            {
                return new[]
                {
                    OperationResourceUtilities.CreateTouch(state.TargetResource),
                };
            }

            return new[]
            {
                OperationResourceUtilities.CreateTouch(state.TargetResource),
                OperationResourceUtilities.CreateTouch(state.ParentResource),
            };
        }

        private static IReadOnlyList<OperationReadInvalidation>? CreateReadInvalidations (OperationResource resource)
        {
            return resource.Kind == OperationTouchKind.Scene
                ? OperationReadInvalidationUtilities.CreateSceneTreeLite(resource.Path)
                : null;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                GameObject parent,
                OperationResource targetResource,
                OperationResource parentResource)
            {
                Target = target;
                Parent = parent;
                TargetResource = targetResource;
                ParentResource = parentResource;
            }

            public GameObject Target { get; }

            public GameObject Parent { get; }

            public OperationResource TargetResource { get; }

            public OperationResource ParentResource { get; }
        }
    }
}