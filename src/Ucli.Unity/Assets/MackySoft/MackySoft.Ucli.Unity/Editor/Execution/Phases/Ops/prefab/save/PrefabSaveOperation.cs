using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
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
            operationName: UcliPrimitiveOperationNames.PrefabSave,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolvePlanArguments(operation, executionContext, out _, out var failure))
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
            if (!TryResolvePlanArguments(
                operation,
                executionContext,
                out var resolutionState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var resource = new OperationResource(OperationTouchKind.Prefab, resolutionState.PrefabPath);
            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(resource);
            var hasDirtyPrefab = resolutionState.PrefabContentsRoot.scene.isDirty;
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: hasRequestAttributedChange || hasDirtyPrefab,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(resource),
                }));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveCallArguments(
                operation,
                executionContext,
                out var resolutionState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var resource = new OperationResource(OperationTouchKind.Prefab, resolutionState.PrefabPath);
            var hasRequestAttributedChange = executionContext.HasRequestAttributedChange(resource);
            var hasDirtyPrefab = resolutionState.PrefabContentsRoot.scene.isDirty;
            if (!hasRequestAttributedChange && !hasDirtyPrefab)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(resource),
                    }));
            }

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(resolutionState.PrefabContentsRoot, resolutionState.PrefabPath);
            if (savedPrefab == null)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab could not be saved: {resolutionState.PrefabPath}."));
            }

            if (resolutionState.PrefabStage != null)
            {
                // NOTE: SaveAsPrefabAsset persists the prefab contents, but the opened Prefab Stage can remain dirty
                // when the user's Prefab Auto Save preference is disabled. Clear it explicitly so batchmode cleanup
                // does not depend on per-user editor settings or try to show a save dialog.
                resolutionState.PrefabStage.ClearDirtiness();
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
                },
                readInvalidations: OperationReadInvalidationUtilities.CreateAssetSearchOnly()));
        }

        private static bool TryResolvePlanArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out ResolutionState resolutionState,
            out OperationPhaseStepResult? failure)
        {
            resolutionState = default;
            failure = null;
            if (!TryParseAndValidatePrefabPath(operation, out var prefabPath, out failure))
            {
                return false;
            }

            var hasOpenedStage = PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out var openedPrefabStage, out _);
            if (!hasOpenedStage
                && !executionContext.HasPlannedLivePrefabOpen(prefabPath))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab is not opened: {prefabPath}. Use 'ucli.prefab.open' first.");
                return false;
            }

            if (executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabContentsRoot)
                && temporaryPrefabContentsRoot != null)
            {
                resolutionState = new ResolutionState(prefabPath, temporaryPrefabContentsRoot, openedPrefabStage);
                return true;
            }

            if (!hasOpenedStage)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab plan state is not available: {prefabPath}.");
                return false;
            }

            var prefabContentsRoot = openedPrefabStage!.prefabContentsRoot;
            if (prefabContentsRoot == null)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Opened prefab root is not available: {prefabPath}.");
                return false;
            }

            resolutionState = new ResolutionState(prefabPath, prefabContentsRoot, openedPrefabStage);
            return true;
        }

        private static bool TryResolveCallArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            out ResolutionState resolutionState,
            out OperationPhaseStepResult? failure)
        {
            resolutionState = default;
            failure = null;
            if (!TryParseAndValidatePrefabPath(operation, out var prefabPath, out failure))
            {
                return false;
            }

            if (!PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out var prefabStage, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            var prefabContentsRoot = prefabStage!.prefabContentsRoot;
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

        private static bool TryParseAndValidatePrefabPath (
            NormalizedOperation operation,
            out string prefabPath,
            out OperationPhaseStepResult? failure)
        {
            failure = null;
            if (!PrefabOperationArgumentsCodec.TryParsePathArguments(operation.Args, out prefabPath, out var parseErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parseErrorMessage);
                return false;
            }

            if (!PrefabOperationUtilities.TryEnsurePrefabAssetExists(prefabPath, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

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

            public GameObject PrefabContentsRoot { get; }

            public PrefabStage? PrefabStage { get; }
        }
    }
}