using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.open</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneOpenOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""path"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""path""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.SceneOpen,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: ArgsSchemaJson);

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
            if (!TryValidateArguments(operation, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

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
            if (!TryValidateArguments(operation, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(validationState.ScenePath, out _, out _)
                && !SceneOperationUtilities.TryEnsureCanOpenSceneLive(validationState.ScenePath, out var blockerErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    blockerErrorMessage));
            }

            if (!executionContext.TryGetOrOpenTemporaryScene(validationState.ScenePath, out _, out var sceneErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    sceneErrorMessage));
            }

            executionContext.TrackPlannedLiveSceneOpen(validationState.ScenePath);

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
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
            if (!TryValidateArguments(operation, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (SceneOperationUtilities.TryGetLoadedScene(validationState.ScenePath, out _, out _))
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: true,
                    changed: false,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
                    }));
            }

            if (!SceneOperationUtilities.TryEnsureCanOpenSceneLive(validationState.ScenePath, out var blockerErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    blockerErrorMessage));
            }

            var openedScene = EditorSceneManager.OpenScene(validationState.ScenePath, OpenSceneMode.Single);
            if (!openedScene.IsValid() || !openedScene.isLoaded)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene could not be opened: {validationState.ScenePath}."));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: false,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(new OperationResource(OperationTouchKind.Scene, validationState.ScenePath)),
                }));
        }

        /// <summary> Validates operation arguments and scene path. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="validationState"> The validated operation state when successful. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!SceneOperationArgumentsCodec.TryParsePathArguments(operation.Args, out var scenePath, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(scenePath, out var sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            validationState = new ValidationState(scenePath);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (string scenePath)
            {
                ScenePath = scenePath;
            }

            public string ScenePath { get; }
        }
    }
}
