using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.tree</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneTreePhaseOperation : IUcliOperation
    {
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.scene.tree",
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson:
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "path": { "type": "string", "minLength": 1 },
                "depth": {
                  "type": ["integer", "null"],
                  "minimum": 0
                }
              },
              "required": ["path"]
            }
            """);

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
            if (!TryValidateArguments(operation, out _, out _, out _, out var failure))
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
            if (!TryValidateArguments(operation, out var scenePath, out var scene, out var depth, out var failure))
            {
                return Task.FromResult(failure!);
            }

            TraverseHierarchy(scene, depth);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scenePath),
                }));
        }

        /// <summary> Validates operation arguments and resolves loaded scene. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="scenePath"> The parsed scene path when successful. </param>
        /// <param name="scene"> The resolved loaded scene when successful. </param>
        /// <param name="depth"> The parsed depth. <see langword="null" /> means unlimited. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            out string scenePath,
            out Scene scene,
            out int? depth,
            out OperationPhaseStepResult? failure)
        {
            scenePath = string.Empty;
            scene = default;
            depth = null;
            failure = null;
            if (!SceneOperationArgumentsCodec.TryParseTreeArguments(operation.Args, out scenePath, out depth, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(scenePath, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(scenePath, out scene, out sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            return true;
        }

        /// <summary> Traverses scene hierarchy to enforce depth semantics. </summary>
        /// <param name="scene"> The loaded scene. </param>
        /// <param name="depth"> The requested depth limit. <see langword="null" /> means unlimited. </param>
        private static void TraverseHierarchy (
            Scene scene,
            int? depth)
        {
            var maxDepth = depth ?? int.MaxValue;
            var roots = scene.GetRootGameObjects();
            var stack = new Stack<(UnityEngine.Transform Transform, int Depth)>(roots.Length);
            for (var rootIndex = roots.Length - 1; rootIndex >= 0; rootIndex--)
            {
                stack.Push((roots[rootIndex].transform, 0));
            }

            while (stack.Count > 0)
            {
                var entry = stack.Pop();
                if (entry.Depth >= maxDepth)
                {
                    continue;
                }

                for (var childIndex = entry.Transform.childCount - 1; childIndex >= 0; childIndex--)
                {
                    stack.Push((entry.Transform.GetChild(childIndex), entry.Depth + 1));
                }
            }
        }

    }
}
