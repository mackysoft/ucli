using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.open</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneOpenPhaseOperation : IUcliOperation
    {
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.scene.open",
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson:
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""path"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""path""]
            }");

        /// <summary> Executes validate phase for <c>ucli.scene.open</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateArguments(operation, out var scenePath, out var failure))
            {
                return Task.FromResult(failure!);
            }

            _ = scenePath;
            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.scene.open</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateArguments(operation, out var scenePath, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scenePath),
                }));
        }

        /// <summary> Executes call phase for <c>ucli.scene.open</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateArguments(operation, out var scenePath, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var openedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!openedScene.IsValid() || !openedScene.isLoaded)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene could not be opened: {scenePath}."));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: false,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scenePath),
                }));
        }

        /// <summary> Validates operation arguments and scene path. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="scenePath"> The resolved scene path when successful. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            out string scenePath,
            out OperationPhaseStepResult? failure)
        {
            scenePath = string.Empty;
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

            return true;
        }
    }
}
