using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.go.describe</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoDescribePhaseOperation : IUcliOperation
    {
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.go.describe",
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson:
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "target": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "var": { "type": "string", "minLength": 1 },
                    "globalObjectId": { "type": "string", "minLength": 1 },
                    "scene": { "type": "string", "minLength": 1 },
                    "hierarchyPath": { "type": "string", "minLength": 1 }
                  },
                  "oneOf": [
                    { "required": ["var"] },
                    { "required": ["globalObjectId"] },
                    { "required": ["scene", "hierarchyPath"] }
                  ]
                },
                "depth": {
                  "type": ["integer", "null"],
                  "minimum": 0
                }
              },
              "required": ["target"]
            }
            """);

        /// <summary> Executes validate phase for <c>ucli.go.describe</c>. </summary>
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

        /// <summary> Executes plan phase for <c>ucli.go.describe</c>. </summary>
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
            return Execute(operation, executionContext, applied: false);
        }

        /// <summary> Executes call phase for <c>ucli.go.describe</c>. </summary>
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
            return Execute(operation, executionContext, applied: true);
        }

        /// <summary> Executes the shared plan/call flow. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="applied"> The applied flag for the successful phase result. </param>
        /// <returns> The phase-step result. </returns>
        private static Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied)
        {
            if (!TryValidateArguments(operation, executionContext, out var target, out var scene, out var depth, out var failure))
            {
                return Task.FromResult(failure!);
            }

            _ = GameObjectDescriptionBuilder.Build(target, depth);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scene.path),
                }));
        }

        /// <summary> Validates arguments and resolves the target loaded-scene GameObject. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="target"> The resolved target GameObject when validation succeeds. </param>
        /// <param name="scene"> The owning loaded scene when validation succeeds. </param>
        /// <param name="depth"> The requested depth value when validation succeeds. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out GameObject target,
            out Scene scene,
            out int? depth,
            out OperationPhaseStepResult? failure)
        {
            target = null!;
            scene = default;
            depth = null;
            failure = null;
            if (!GoDescribeArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            depth = parsedArguments.Depth;
            if (!GoOperationUtilities.TryResolveLoadedSceneGameObject(
                parsedArguments.TargetReference,
                executionContext,
                out var resolvedTarget,
                out scene,
                out var targetErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, targetErrorMessage);
                return false;
            }

            target = resolvedTarget!;
            return true;
        }
    }
}
