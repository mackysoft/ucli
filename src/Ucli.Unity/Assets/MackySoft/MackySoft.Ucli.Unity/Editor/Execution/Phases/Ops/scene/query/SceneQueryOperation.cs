using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.scene.query</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class SceneQueryOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""scene"": { ""type"": ""string"", ""minLength"": 1 },
                ""pathPrefix"": { ""type"": ""string"", ""minLength"": 1 },
                ""componentType"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""scene""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.SceneQuery,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, out _, out _, out var failure))
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
            return Execute(operation, executionContext, applied: false);
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Execute(operation, executionContext, applied: true);
        }

        private static Task<OperationPhaseStepResult> Execute (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool applied)
        {
            if (!TryValidate(operation, out var scenePath, out var queryArguments, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!SceneQuerySelectionEngine.TryQueryRuntime(
                scenePath,
                queryArguments,
                executionContext,
                allowTemporaryState: !applied,
                out var matches,
                out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            var payload = new SceneQueryResult(
                Scene: scenePath,
                Matches: CreatePayloadMatches(matches));
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: applied,
                changed: false,
                touched: new[]
                {
                    SceneOperationUtilities.CreateSceneTouch(scenePath),
                },
                result: IpcPayloadCodec.SerializeToElement(payload)));
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            out string scenePath,
            out SceneQuerySelectionEngine.QueryArguments queryArguments,
            out OperationPhaseStepResult? failure)
        {
            scenePath = string.Empty;
            queryArguments = default;
            failure = null;
            if (!SceneQuerySelectionEngine.TryParseOpArgs(operation.Args, out scenePath, out queryArguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (queryArguments.ComponentType != null
                && !ComponentTypeResolver.TryResolveComponentType(queryArguments.ComponentType, out _, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            return true;
        }

        private static IReadOnlyList<SceneQueryMatchPayload> CreatePayloadMatches (
            IReadOnlyList<SceneQuerySelectionEngine.QueryMatch> matches)
        {
            var payloadMatches = new SceneQueryMatchPayload[matches.Count];
            for (var i = 0; i < matches.Count; i++)
            {
                payloadMatches[i] = new SceneQueryMatchPayload(
                    Kind: matches[i].TargetKind == IpcEditTargetKind.Component ? "component" : "gameObject",
                    HierarchyPath: matches[i].HierarchyPath,
                    ComponentType: matches[i].ComponentType);
            }

            return payloadMatches;
        }

        private sealed record SceneQueryResult (
            string Scene,
            IReadOnlyList<SceneQueryMatchPayload> Matches);

        private sealed record SceneQueryMatchPayload (
            string Kind,
            string HierarchyPath,
            string? ComponentType);
    }
}
