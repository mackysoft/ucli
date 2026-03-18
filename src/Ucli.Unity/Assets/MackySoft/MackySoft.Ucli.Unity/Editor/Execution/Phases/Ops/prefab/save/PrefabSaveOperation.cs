using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.prefab.save</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class PrefabSaveOperation : IUcliOperation
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
            operationName: "ucli.prefab.save",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveArguments(operation, executionContext, allowTemporaryState: true, out _, out var failure))
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
            if (!TryResolveArguments(
                operation,
                executionContext,
                allowTemporaryState: true,
                out var resolutionState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: resolutionState.PrefabContentsRoot!.scene.isDirty,
                touched: new[]
                {
                    PrefabOperationUtilities.CreatePrefabTouch(resolutionState.PrefabPath),
                }));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveArguments(
                operation,
                executionContext,
                allowTemporaryState: false,
                out var resolutionState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var changedBeforeSave = resolutionState.PrefabContentsRoot!.scene.isDirty;
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(resolutionState.PrefabContentsRoot, resolutionState.PrefabPath);
            if (savedPrefab == null)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab could not be saved: {resolutionState.PrefabPath}."));
            }

            if (resolutionState.PrefabStage != null)
            {
                resolutionState.PrefabStage.ClearDirtiness();
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: changedBeforeSave,
                touched: new[]
                {
                    PrefabOperationUtilities.CreatePrefabTouch(resolutionState.PrefabPath),
                }));
        }

        private static bool TryResolveArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ResolutionState resolutionState,
            out OperationPhaseStepResult? failure)
        {
            resolutionState = default;
            failure = null;
            if (!PrefabOperationArgumentsCodec.TryParsePathArguments(operation.Args, out var prefabPath, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            if (!PrefabOperationUtilities.TryEnsurePrefabAssetExists(prefabPath, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (allowTemporaryState
                && executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var prefabContentsRoot))
            {
                resolutionState = new ResolutionState(prefabPath, prefabContentsRoot!, null);
                return true;
            }

            if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out var prefabStage, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            prefabContentsRoot = prefabStage!.prefabContentsRoot;
            if (prefabContentsRoot == null)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Opened prefab root is not available: {prefabPath}.");
                return false;
            }

            resolutionState = new ResolutionState(prefabPath, prefabContentsRoot, prefabStage);
            return true;
        }

        private readonly struct ResolutionState
        {
            public ResolutionState (
                string prefabPath,
                GameObject prefabContentsRoot,
                PrefabStage? prefabStage)
            {
                PrefabPath = prefabPath;
                PrefabContentsRoot = prefabContentsRoot;
                PrefabStage = prefabStage;
            }

            public string PrefabPath { get; }

            public GameObject? PrefabContentsRoot { get; }

            public PrefabStage? PrefabStage { get; }
        }
    }
}