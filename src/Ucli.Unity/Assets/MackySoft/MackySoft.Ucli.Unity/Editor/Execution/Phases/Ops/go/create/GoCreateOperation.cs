using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.go.create</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoCreateOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""name"": { ""type"": ""string"", ""minLength"": 1 },
                ""scene"": { ""type"": ""string"", ""minLength"": 1 },
                ""parent"": {
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
                }
              },
              ""required"": [""name""],
              ""oneOf"": [
                { ""required"": [""scene""] },
                { ""required"": [""parent""] }
              ]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.go.create",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        /// <summary> Executes validate phase for <c>ucli.go.create</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
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

        /// <summary> Executes plan phase for <c>ucli.go.create</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(
                operation,
                executionContext,
                allowTemporaryState: true,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (operation.As != null)
            {
                var temporaryGameObject = GoOperationUtilities.CreateTemporaryGameObject(validationState.Name, executionContext);
                TryParentTemporaryGameObject(validationState, executionContext, temporaryGameObject);
                executionContext.SetTemporaryAlias(operation.As, temporaryGameObject, validationState.Resource);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(validationState.Resource),
                }));
        }

        /// <summary> Executes call phase for <c>ucli.go.create</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(
                operation,
                executionContext,
                allowTemporaryState: false,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var createdGameObject = new GameObject(validationState.Name);
            if (validationState.Parent != null)
            {
                SceneManager.MoveGameObjectToScene(createdGameObject, validationState.Parent.scene);
                createdGameObject.transform.SetParent(validationState.Parent.transform, worldPositionStays: false);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(createdGameObject, validationState.Scene);
            }

            StoreAliasIfNeeded(operation.As, executionContext, createdGameObject, validationState.Resource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: true,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(validationState.Resource),
                }));
        }

        /// <summary> Validates arguments and resolves the destination resource and optional parent. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan aliases may satisfy parent resolution. </param>
        /// <param name="validationState"> The validated operation state when validation succeeds. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!GoCreateArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            if (!parsedArguments.HasParentReference)
            {
                if (!GoOperationUtilities.TryResolveLoadedScene(parsedArguments.ScenePath!, out var scene, out var sceneErrorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                    return false;
                }

                validationState = new ValidationState(
                    parsedArguments,
                    parsedArguments.Name,
                    scene,
                    parent: null,
                    new OperationResource(OperationTouchKind.Scene, scene.path));
                return true;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                parsedArguments.ParentReference,
                executionContext,
                allowTemporaryState,
                out var parentResolution,
                out var parentErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parentErrorMessage);
                return false;
            }

            validationState = new ValidationState(
                parsedArguments,
                parsedArguments.Name,
                default,
                parentResolution.GameObject,
                parentResolution.Resource);
            return true;
        }

        /// <summary> Parents one temporary GameObject under one temporary parent when the plan uses temporary aliases. </summary>
        /// <param name="validationState"> The validated operation state. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="temporaryGameObject"> The temporary GameObject created for plan-time aliasing. </param>
        private static void TryParentTemporaryGameObject (
            ValidationState validationState,
            OperationExecutionContext executionContext,
            GameObject temporaryGameObject)
        {
            if (!validationState.ParsedArguments.HasParentReference
                || validationState.Parent == null
                || validationState.ParsedArguments.ParentReference.Kind != UnityObjectReferenceKind.Alias)
            {
                return;
            }

            if (!executionContext.TryGetTemporaryAliasState(validationState.ParsedArguments.ParentReference.Alias!, out var temporaryParentState))
            {
                return;
            }

            var temporaryParent = temporaryParentState.UnityObject as GameObject;
            if (temporaryParent == null)
            {
                return;
            }

            temporaryGameObject.transform.SetParent(temporaryParent.transform, worldPositionStays: false);
        }

        /// <summary> Stores one alias for the created GameObject when the request specifies <c>as</c>. </summary>
        /// <param name="alias"> The optional alias name. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="createdGameObject"> The created GameObject. </param>
        /// <param name="resource"> The owner resource for the created GameObject. </param>
        private static void StoreAliasIfNeeded (
            string? alias,
            OperationExecutionContext executionContext,
            GameObject createdGameObject,
            OperationResource resource)
        {
            if (alias == null)
            {
                return;
            }

            executionContext.SetTemporaryAlias(alias, createdGameObject, resource);
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(createdGameObject, out var resolvedReference))
            {
                executionContext.AliasStore.Set(alias, resolvedReference!);
            }
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GoCreateArguments parsedArguments,
                string name,
                Scene scene,
                GameObject? parent,
                OperationResource resource)
            {
                ParsedArguments = parsedArguments;
                Name = name;
                Scene = scene;
                Parent = parent;
                Resource = resource;
            }

            public GoCreateArguments ParsedArguments { get; }

            public string Name { get; }

            public Scene Scene { get; }

            public GameObject? Parent { get; }

            public OperationResource Resource { get; }
        }
    }
}