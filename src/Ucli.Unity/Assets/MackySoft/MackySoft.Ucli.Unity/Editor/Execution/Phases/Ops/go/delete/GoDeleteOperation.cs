using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.go.delete</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoDeleteOperation : IUcliOperation
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
                }
              },
              ""required"": [""target""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.GoDelete,
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
                    state.Resource,
                    executionContext,
                    out var preparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    preparationErrorMessage));
            }

            if (!TryValidate(operation, executionContext, allowTemporaryState: true, out state, out failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                state.Target,
                state.Resource,
                executionContext,
                out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            RegisterDeletedGlobalObjectIds(state.Target, state.Resource, executionContext);
            Object.DestroyImmediate(state.Target);
            GoOperationUtilities.MarkPlanResourceDirty(state.Resource, executionContext);
            executionContext.MarkRequestAttributedChange(state.Resource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(state.Resource),
                }));
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

            Object.DestroyImmediate(state.Target);
            executionContext.MarkRequestAttributedChange(state.Resource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: true,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(state.Resource),
                }));
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
            if (!operation.Args.TryGetProperty(GoOperationPropertyNames.Target, out var targetElement))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, "Operation 'args' requires property 'target'.");
                return false;
            }

            if (!UnityObjectReferenceCodec.TryParse(targetElement, "args.target", out var targetReference, out var errorMessage))
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

            if (!TryValidateDeletionTarget(
                    targetResolution.GameObject!,
                    targetResolution.Resource,
                    executionContext,
                    out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            state = new ValidationState(targetResolution.GameObject!, targetResolution.Resource);
            return true;
        }

        private static bool TryValidateDeletionTarget (
            GameObject target,
            OperationResource resource,
            OperationExecutionContext executionContext,
            out string errorMessage)
        {
            if (resource.Kind != OperationTouchKind.Prefab)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (executionContext.TryGetTemporaryPrefabContentsRoot(resource.Path, out var temporaryPrefabContentsRoot)
                && temporaryPrefabContentsRoot != null
                && target == temporaryPrefabContentsRoot)
            {
                errorMessage = "Prefab root cannot be deleted.";
                return false;
            }

            if (PrefabOperationUtilities.TryGetOpenedPrefabStage(resource.Path, out var openedPrefabStage, out _))
            {
                var prefabContentsRoot = openedPrefabStage!.prefabContentsRoot;
                if (prefabContentsRoot != null
                    && target == prefabContentsRoot)
                {
                    errorMessage = "Prefab root cannot be deleted.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static void RegisterDeletedGlobalObjectIds (
            GameObject target,
            OperationResource resource,
            OperationExecutionContext executionContext)
        {
            RegisterDeletedGlobalObjectId(target, resource, executionContext);
            var components = target.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                RegisterDeletedGlobalObjectId(component, resource, executionContext);
            }

            var childCount = target.transform.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = target.transform.GetChild(i);
                RegisterDeletedGlobalObjectIds(child.gameObject, resource, executionContext);
            }
        }

        private static void RegisterDeletedGlobalObjectId (
            UnityEngine.Object unityObject,
            OperationResource resource,
            OperationExecutionContext executionContext)
        {
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(unityObject, out var directReference))
            {
                executionContext.MarkDeletedGlobalObjectId(directReference!.GlobalObjectId);
            }

            switch (resource.Kind)
            {
                case OperationTouchKind.Scene:
                    if (executionContext.TryResolveTemporarySceneSourceObject(resource.Path, unityObject, out var sourceSceneObject)
                        && sourceSceneObject != null
                        && UnityObjectReferenceResolver.TryCreateResolvedReference(sourceSceneObject, out var sourceSceneReference))
                    {
                        executionContext.MarkDeletedGlobalObjectId(sourceSceneReference!.GlobalObjectId);
                    }

                    break;

                case OperationTouchKind.Prefab:
                    if (executionContext.TryResolveTemporaryPrefabSourceObject(resource.Path, unityObject, out var sourcePrefabObject)
                        && sourcePrefabObject != null
                        && UnityObjectReferenceResolver.TryCreateResolvedReference(sourcePrefabObject, out var sourcePrefabReference))
                    {
                        executionContext.MarkDeletedGlobalObjectId(sourcePrefabReference!.GlobalObjectId);
                    }

                    break;
            }
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                OperationResource resource)
            {
                Target = target;
                Resource = resource;
            }

            public GameObject Target { get; }

            public OperationResource Resource { get; }
        }
    }
}
