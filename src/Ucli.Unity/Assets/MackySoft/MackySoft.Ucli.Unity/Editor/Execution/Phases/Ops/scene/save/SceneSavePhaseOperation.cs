using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.save</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneSavePhaseOperation : IUcliOperation
    {
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.scene.save",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson:
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "path": { "type": "string", "minLength": 1 }
              },
              "required": ["path"]
            }
            """);

        /// <summary> Executes validate phase for <c>ucli.scene.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateArguments(operation, out _, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.scene.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateArguments(operation, out var scenePath, out var scene, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: scene.isDirty,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scenePath),
                }));
        }

        /// <summary> Executes call phase for <c>ucli.scene.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateArguments(operation, out var scenePath, out var scene, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var changedBeforeSave = scene.isDirty;
            if (!EditorSceneManager.SaveScene(scene))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene could not be saved: {scenePath}."));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: changedBeforeSave,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scenePath),
                }));
        }

        /// <summary> Validates operation arguments and resolves loaded scene. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="scenePath"> The parsed scene path when successful. </param>
        /// <param name="scene"> The resolved loaded scene when successful. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            out string scenePath,
            out Scene scene,
            out OperationPhaseStepResult? failure)
        {
            scenePath = string.Empty;
            scene = default;
            failure = null;
            if (!SceneOperationArgumentsCodec.TryParsePathArguments(operation.Args, out scenePath, out var parseErrorMessage))
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
    }
}
