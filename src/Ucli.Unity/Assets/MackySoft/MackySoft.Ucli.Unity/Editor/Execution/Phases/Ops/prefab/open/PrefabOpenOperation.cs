using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.prefab.open</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class PrefabOpenOperation : IUcliOperation
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
            operationName: UcliPrimitiveOperationNames.PrefabOpen,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, out _, out var failure))
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
            if (!TryValidateArguments(operation, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            GameObject? prefabContentsRoot;
            if (!PrefabOperationUtilities.TryGetOrLoadTemporaryPrefabContentsRoot(
                validationState.PrefabPath,
                executionContext,
                out prefabContentsRoot,
                out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(
                    operation.As,
                    prefabContentsRoot!,
                    OperationResource.Prefab(validationState.PrefabPath));
            }

            executionContext.TrackPlannedLivePrefabOpen(validationState.PrefabPath);

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                touched: new[]
                {
                    PrefabOperationUtilities.CreatePrefabTouch(validationState.PrefabPath),
                }));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, out var validationState, out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!PrefabOperationUtilities.TryOpenPrefabStage(validationState.PrefabPath, out var prefabStage, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            var prefabContentsRoot = prefabStage!.prefabContentsRoot;
            if (prefabContentsRoot == null)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab root is not available after open: {validationState.PrefabPath}."));
            }

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(
                    operation.As,
                    prefabContentsRoot,
                    OperationResource.Prefab(validationState.PrefabPath));
                if (UnityObjectReferenceResolver.TryCreateResolvedReference(prefabContentsRoot, out var resolvedReference))
                {
                    executionContext.AliasStore.Set(operation.As, resolvedReference!);
                }
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: false,
                touched: new[]
                {
                    PrefabOperationUtilities.CreatePrefabTouch(validationState.PrefabPath),
                }));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
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

            validationState = new ValidationState(prefabPath);
            return true;
        }

        private readonly struct ValidationState
        {
            public ValidationState (string prefabPath)
            {
                PrefabPath = prefabPath;
            }

            public string PrefabPath { get; }
        }
    }
}
