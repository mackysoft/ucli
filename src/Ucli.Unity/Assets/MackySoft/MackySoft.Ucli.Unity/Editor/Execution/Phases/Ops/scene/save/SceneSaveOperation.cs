using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.save</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneSaveOperation : IUcliOperation
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
            operationName: UcliPrimitiveOperationNames.SceneSave,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

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
            if (!TryValidateArguments(operation, executionContext, allowTemporaryState: true, out _, out var failure))
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
            if (!TryValidateArguments(operation, executionContext, allowTemporaryState: true, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            var resource = new OperationResource(OperationTouchKind.Scene, validationState.ScenePath);
            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(resource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: hasRequestAttributedChange || validationState.Scene.isDirty,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(resource),
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
            if (!TryValidateArguments(operation, executionContext, allowTemporaryState: false, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (EditorSceneManager.IsPreviewScene(validationState.Scene))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene is not loaded: {validationState.ScenePath}. Use 'ucli.scene.open' first."));
            }

            var resource = new OperationResource(OperationTouchKind.Scene, validationState.ScenePath);
            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(resource);
            if (!hasRequestAttributedChange
                && !validationState.Scene.isDirty)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(resource),
                    }));
            }

            if (!EditorSceneManager.SaveScene(validationState.Scene, validationState.ScenePath))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Scene could not be saved: {validationState.ScenePath}."));
            }

            if (hasRequestAttributedChange)
            {
                executionContext.UnmarkRequestAttributedChange(resource);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: true,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(resource),
                }));
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

            Scene scene;
            if (allowTemporaryState
                && executionContext.TryGetTemporaryScene(scenePath, out scene))
            {
                validationState = new ValidationState(scenePath, scene);
                return true;
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(scenePath, out scene, out sceneErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                return false;
            }

            validationState = new ValidationState(scenePath, scene);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                string scenePath,
                Scene scene)
            {
                ScenePath = scenePath;
                Scene = scene;
            }

            public string ScenePath { get; }

            public Scene Scene { get; }
        }
    }
}
