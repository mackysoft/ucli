using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Project;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.project.refresh</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class ProjectRefreshPhaseOperation : IUcliOperation
    {
        private const string ArgsSchemaJson =
            @"{
              ""type"": ""object"",
              ""additionalProperties"": false
            }";

        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: UcliPrimitiveOperationNames.ProjectRefresh,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced,
            argsSchemaJson: ArgsSchemaJson);

        /// <summary> Executes validate phase for <c>ucli.project.refresh</c>. </summary>
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
            if (!ProjectOperationUtilities.TryValidateEmptyArguments(operation.Args, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.project.refresh</c>. </summary>
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
            if (!ProjectOperationUtilities.TryValidateEmptyArguments(operation.Args, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes call phase for <c>ucli.project.refresh</c>. </summary>
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
            if (!ProjectOperationUtilities.TryValidateEmptyArguments(operation.Args, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
            var beforeSnapshot = ProjectOperationUtilities.CaptureProjectSettingsSnapshot(projectRoot);
            var beforeSceneDirtyState = CaptureLoadedSceneDirtyState();
            var beforePrefabDirtyState = CaptureOpenedPrefabStageDirtyState();
            var scopeId = ProjectOperationCallbackRegistry.BeginRefreshCapture();
            IReadOnlyList<string> callbackPaths;
            try
            {
                AssetDatabase.Refresh();
            }
            finally
            {
                callbackPaths = ProjectOperationCallbackRegistry.EndRefreshCapture(scopeId);
            }

            var afterSnapshot = ProjectOperationUtilities.CaptureProjectSettingsSnapshot(projectRoot);
            var afterSceneDirtyState = CaptureLoadedSceneDirtyState();
            var afterPrefabDirtyState = CaptureOpenedPrefabStageDirtyState();
            var changedProjectSettingsPaths = ProjectOperationUtilities.GetChangedProjectSettingsPaths(beforeSnapshot, afterSnapshot);
            var touched = new List<OperationTouch>(ProjectOperationUtilities.CreateTouchedResources(callbackPaths, changedProjectSettingsPaths));
            ProjectOperationUtilities.SyncDirtyStateChanges(
                beforeSceneDirtyState,
                afterSceneDirtyState,
                OperationTouchKind.Scene,
                touched,
                executionContext);
            ProjectOperationUtilities.SyncDirtyStateChanges(
                beforePrefabDirtyState,
                afterPrefabDirtyState,
                OperationTouchKind.Prefab,
                touched,
                executionContext);
            var deduplicatedTouched = DeduplicateTouched(touched);
            MarkRequestAttributedChanges(deduplicatedTouched, executionContext);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: deduplicatedTouched.Count != 0,
                touched: deduplicatedTouched,
                readInvalidations: deduplicatedTouched.Count == 0
                    ? null
                    : CreateReadInvalidations(callbackPaths, deduplicatedTouched)));
        }

        private static Dictionary<string, bool> CaptureLoadedSceneDirtyState ()
        {
            var dirtyStateByPath = new Dictionary<string, bool>(System.StringComparer.Ordinal);
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid()
                    || !scene.isLoaded
                    || string.IsNullOrWhiteSpace(scene.path)
                    || EditorSceneManager.IsPreviewScene(scene))
                {
                    continue;
                }

                dirtyStateByPath[scene.path] = scene.isDirty;
            }

            return dirtyStateByPath;
        }

        private static Dictionary<string, bool> CaptureOpenedPrefabStageDirtyState ()
        {
            var dirtyStateByPath = new Dictionary<string, bool>(System.StringComparer.Ordinal);
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null
                || string.IsNullOrWhiteSpace(prefabStage.assetPath))
            {
                return dirtyStateByPath;
            }

            var prefabRoot = prefabStage.prefabContentsRoot;
            if (prefabRoot == null)
            {
                return dirtyStateByPath;
            }

            dirtyStateByPath[prefabStage.assetPath] = prefabRoot.scene.isDirty;
            return dirtyStateByPath;
        }

        private static void MarkRequestAttributedChanges (
            IReadOnlyList<OperationTouch> touched,
            OperationExecutionContext executionContext)
        {
            for (var i = 0; i < touched.Count; i++)
            {
                var touch = touched[i];
                switch (touch.Kind)
                {
                    case OperationTouchKind.ProjectSettings:
                    case OperationTouchKind.Asset:
                        executionContext.MarkRequestAttributedChange(new OperationResource(touch.Kind, touch.Path));
                        break;
                }
            }
        }

        private static IReadOnlyList<OperationTouch> DeduplicateTouched (IReadOnlyList<OperationTouch> touched)
        {
            var touchedByPath = new SortedDictionary<string, OperationTouch>(System.StringComparer.Ordinal);
            for (var i = 0; i < touched.Count; i++)
            {
                var current = touched[i];
                if (touchedByPath.TryGetValue(current.Path, out var existing)
                    && existing.Guid != null)
                {
                    continue;
                }

                touchedByPath[current.Path] = current;
            }

            var result = new OperationTouch[touchedByPath.Count];
            var resultIndex = 0;
            foreach (var touch in touchedByPath.Values)
            {
                result[resultIndex] = touch;
                resultIndex++;
            }

            return result;
        }

        private static IReadOnlyList<OperationReadInvalidation> CreateReadInvalidations (
            IReadOnlyList<string> callbackPaths,
            IReadOnlyList<OperationTouch> touched)
        {
            var invalidations = new List<OperationReadInvalidation>();
            var includesAssetLookupInvalidation = false;
            var callbackTouched = ProjectOperationUtilities.CreateTouchedResources(callbackPaths, System.Array.Empty<string>());
            for (var i = 0; i < callbackTouched.Count; i++)
            {
                var touch = callbackTouched[i];
                if (touch.Kind == OperationTouchKind.ProjectSettings)
                {
                    continue;
                }

                if (!includesAssetLookupInvalidation)
                {
                    invalidations.AddRange(OperationReadInvalidationUtilities.CreateAssetSearchAndGuidPath());
                    includesAssetLookupInvalidation = true;
                }
            }

            for (var i = 0; i < touched.Count; i++)
            {
                if (touched[i].Kind != OperationTouchKind.Scene)
                {
                    continue;
                }

                invalidations.AddRange(OperationReadInvalidationUtilities.CreateSceneTreeLite(touched[i].Path));
            }

            return invalidations;
        }
    }
}