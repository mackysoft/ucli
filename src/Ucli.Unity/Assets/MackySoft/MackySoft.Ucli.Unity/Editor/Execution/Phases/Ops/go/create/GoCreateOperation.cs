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
    internal sealed class GoCreatePhaseOperation : IUcliOperation
    {
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.go.create",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson:
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
            }");

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
            if (!TryValidateArguments(operation, executionContext, out _, out _, out _, out var failure))
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
            if (!TryValidateArguments(operation, executionContext, out _, out var scene, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scene.path),
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
            if (!TryValidateArguments(operation, executionContext, out var name, out var scene, out var parent, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var createdGameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(createdGameObject, scene);
            if (parent != null)
            {
                createdGameObject.transform.SetParent(parent.transform, worldPositionStays: false);
            }

            StoreAliasIfNeeded(operation.As, executionContext, createdGameObject);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: true,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scene.path),
                }));
        }

        /// <summary> Validates arguments and resolves the destination scene and optional parent. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="name"> The GameObject name when validation succeeds. </param>
        /// <param name="scene"> The destination loaded scene when validation succeeds. </param>
        /// <param name="parent"> The optional parent GameObject when validation succeeds. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out string name,
            out Scene scene,
            out GameObject? parent,
            out OperationPhaseStepResult? failure)
        {
            name = string.Empty;
            scene = default;
            parent = null;
            failure = null;
            if (!GoCreateArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            name = parsedArguments.Name;
            if (!parsedArguments.HasParentReference)
            {
                if (!GoOperationUtilities.TryResolveLoadedScene(parsedArguments.ScenePath!, out scene, out var sceneErrorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                    return false;
                }

                return true;
            }

            if (!GoOperationUtilities.TryResolveLoadedSceneGameObject(
                parsedArguments.ParentReference,
                executionContext,
                out parent,
                out scene,
                out var parentErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parentErrorMessage);
                return false;
            }

            return true;
        }

        /// <summary> Stores one alias for the created GameObject when the request specifies <c>as</c>. </summary>
        /// <param name="alias"> The optional alias name. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="createdGameObject"> The created GameObject. </param>
        private static void StoreAliasIfNeeded (
            string? alias,
            OperationExecutionContext executionContext,
            GameObject createdGameObject)
        {
            if (alias == null)
            {
                return;
            }

            executionContext.AliasStore.Set(alias, UnityObjectReferenceResolver.CreateResolvedReference(createdGameObject));
        }
    }
}
