using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.tree</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneTreeOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""path"": { ""type"": ""string"", ""minLength"": 1 },
                ""depth"": {
                  ""type"": [""integer"", ""null""],
                  ""minimum"": 0
                }
              },
              ""required"": [""path""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.SceneTree,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: ArgsSchemaJson);

        /// <summary> Executes validate phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateArguments(operation, executionContext, allowTemporaryState: true, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            return Execute(operation, executionContext, applied: false);
        }

        /// <summary> Executes call phase for <c>ucli.scene.tree</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            return Execute(operation, executionContext, applied: true);
        }

        /// <summary> Executes shared plan/call flow. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="applied"> The applied flag for success. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied)
        {
            if (!TryValidateArguments(operation, executionContext, allowTemporaryState: !applied, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var tree = BuildSceneTree(validationState.ScenePath, validationState.Scene, validationState.Depth);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
                },
                result: IpcPayloadCodec.SerializeToElement(tree)));
        }

        /// <summary> Validates operation arguments and resolves loaded scene. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="validationState"> The validated operation state when successful. </param>
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
            if (!SceneOperationArgumentsCodec.TryParseTreeArguments(operation.Args, out var parsedArguments, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            var scenePath = parsedArguments.ScenePath;
            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(scenePath, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveScene(scenePath, executionContext, allowTemporaryState, out var scene, out sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            validationState = new ValidationState(scenePath, scene, parsedArguments.Depth);
            return true;
        }

        /// <summary> Builds one deterministic scene-tree payload. </summary>
        /// <param name="scenePath"> The loaded scene path. </param>
        /// <param name="scene"> The loaded scene. </param>
        /// <param name="depth"> The requested depth limit. <see langword="null" /> means unlimited. </param>
        /// <returns> The scene-tree payload. </returns>
        private static SceneTreeDescription BuildSceneTree (
            string scenePath,
            Scene scene,
            int? depth)
        {
            var maxDepth = depth ?? int.MaxValue;
            var roots = scene.GetRootGameObjects();
            var rootDescriptions = new SceneTreeNodeDescription[roots.Length];
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                rootDescriptions[rootIndex] = BuildNode(roots[rootIndex], currentDepth: 0, maxDepth);
            }

            return new SceneTreeDescription(scenePath, rootDescriptions);
        }

        /// <summary> Builds one tree node and its children. </summary>
        /// <param name="gameObject"> The source GameObject. </param>
        /// <param name="currentDepth"> The current depth from the scene root. </param>
        /// <param name="maxDepth"> The maximum depth to include. </param>
        /// <returns> The built tree node. </returns>
        private static SceneTreeNodeDescription BuildNode (
            GameObject gameObject,
            int currentDepth,
            int maxDepth)
        {
            var children = currentDepth >= maxDepth
                ? System.Array.Empty<SceneTreeNodeDescription>()
                : BuildChildren(gameObject.transform, currentDepth + 1, maxDepth);
            var globalObjectId = UnityObjectReferenceResolver.TryCreateResolvedReference(gameObject, out var resolvedReference)
                ? resolvedReference!.GlobalObjectId
                : string.Empty;
            return new SceneTreeNodeDescription(
                Name: gameObject.name,
                GlobalObjectId: globalObjectId,
                Children: children);
        }

        /// <summary> Builds child nodes for one transform. </summary>
        /// <param name="transform"> The parent transform. </param>
        /// <param name="childDepth"> The child depth. </param>
        /// <param name="maxDepth"> The maximum depth to include. </param>
        /// <returns> The child node list. </returns>
        private static IReadOnlyList<SceneTreeNodeDescription> BuildChildren (
            Transform transform,
            int childDepth,
            int maxDepth)
        {
            if (transform.childCount == 0)
            {
                return System.Array.Empty<SceneTreeNodeDescription>();
            }

            var children = new SceneTreeNodeDescription[transform.childCount];
            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                children[childIndex] = BuildNode(transform.GetChild(childIndex).gameObject, childDepth, maxDepth);
            }

            return children;
        }

        private sealed record SceneTreeDescription (
            string Path,
            IReadOnlyList<SceneTreeNodeDescription> Roots);

        private sealed record SceneTreeNodeDescription (
            string Name,
            string GlobalObjectId,
            IReadOnlyList<SceneTreeNodeDescription> Children);

        private readonly struct ValidationState
        {
            public ValidationState (
                string scenePath,
                Scene scene,
                int? depth)
            {
                ScenePath = scenePath;
                Scene = scene;
                Depth = depth;
            }

            public string ScenePath { get; }

            public Scene Scene { get; }

            public int? Depth { get; }
        }
    }
}
