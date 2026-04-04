using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.prefab.create</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class PrefabCreateOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""properties"": {
                ""target"": {
                  ""type"": ""object"",
                  ""additionalProperties"": false,
                  ""properties"": {
                    ""var"": { ""type"": ""string"", ""minLength"": 1 },
                    ""globalObjectId"": { ""type"": ""string"", ""minLength"": 1 },
                    ""scene"": { ""type"": ""string"", ""minLength"": 1 },
                    ""hierarchyPath"": { ""type"": ""string"", ""minLength"": 1 }
                  },
                  ""oneOf"": [
                    { ""required"": [""var""] },
                    { ""required"": [""globalObjectId""] },
                    { ""required"": [""scene"", ""hierarchyPath""] }
                  ]
                },
                ""path"": { ""type"": ""string"", ""minLength"": 1 }
              },
              ""required"": [""target"", ""path""]
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.PrefabCreate,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, executionContext, allowTemporaryState: true, out _, out var failure))
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
            if (!TryValidateArguments(
                operation,
                executionContext,
                allowTemporaryState: true,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(operation.As, validationState.Target, validationState.SourceResource);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: OperationResourceUtilities.CreateTouches(
                    validationState.SourceResource,
                    OperationResource.Prefab(validationState.PrefabPath))));
        }

        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(
                operation,
                executionContext,
                allowTemporaryState: false,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var prefabAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(validationState.Target, validationState.PrefabPath, InteractionMode.AutomatedAction);
            if (prefabAsset == null)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Prefab could not be created: {validationState.PrefabPath}."));
            }

            StoreAliasIfNeeded(operation.As, executionContext, validationState.Target, validationState.SourceResource);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: true,
                touched: OperationResourceUtilities.CreateTouches(
                    validationState.SourceResource,
                    OperationResource.Prefab(validationState.PrefabPath))));
        }

        private static bool TryValidateArguments (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;
            if (!PrefabCreateArgumentsCodec.TryParse(operation.Args, out var parsedArguments, out var errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                parsedArguments.TargetReference,
                executionContext,
                allowTemporaryState,
                out var targetResolution,
                out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            if (targetResolution.Resource.Kind != OperationTouchKind.Scene)
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Prefab source must be a GameObject that belongs to a loaded scene.");
                return false;
            }

            if (!PrefabOperationUtilities.TryEnsurePrefabAssetCanBeCreated(parsedArguments.PrefabPath, out errorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage);
                return false;
            }

            validationState = new ValidationState(
                targetResolution.GameObject!,
                targetResolution.Resource,
                parsedArguments.PrefabPath);
            return true;
        }

        private static void StoreAliasIfNeeded (
            string? alias,
            OperationExecutionContext executionContext,
            GameObject target,
            OperationResource resource)
        {
            if (alias == null)
            {
                return;
            }

            executionContext.SetTemporaryAlias(alias, target, resource);
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(target, out var resolvedReference))
            {
                executionContext.AliasStore.Set(alias, resolvedReference!);
            }
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                GameObject target,
                OperationResource sourceResource,
                string prefabPath)
            {
                Target = target;
                SourceResource = sourceResource;
                PrefabPath = prefabPath;
            }

            public GameObject? Target { get; }

            public OperationResource SourceResource { get; }

            public string PrefabPath { get; }
        }
    }
}
