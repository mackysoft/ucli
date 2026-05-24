using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Project;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.project.refresh</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class ProjectRefreshPhaseOperation : UcliOperation<UcliEmptyArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.ProjectRefresh,
            kind: UcliOperationKind.Command,
            description: "Refreshes Unity AssetDatabase and reports resources changed by import.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[]
                {
                    UcliOperationSideEffect.AssetDatabaseRefresh,
                    UcliOperationSideEffect.AssetImport,
                    UcliOperationSideEffect.ScriptCompilation,
                    UcliOperationSideEffect.DomainReload,
                    UcliOperationSideEffect.SceneContentMutation,
                    UcliOperationSideEffect.PrefabContentMutation,
                    UcliOperationSideEffect.AssetContentMutation,
                    UcliOperationSideEffect.ProjectSettingsMutation,
                    UcliOperationSideEffect.AssetSave,
                },
                touchedKinds: new[]
                {
                    UcliTouchedResourceKindNames.Scene,
                    UcliTouchedResourceKindNames.Prefab,
                    UcliTouchedResourceKindNames.Asset,
                    UcliTouchedResourceKindNames.ProjectSettings,
                },
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate that AssetDatabase refresh can be requested; plan does not run import or observe refreshed project state.",
                callSemantics: "Run Unity AssetDatabase refresh/import and report resources Unity exposes as changed by the import pass.",
                touchedContract: "Reports assets, scenes, prefabs, or ProjectSettings observed through Unity refresh callbacks and dirty-state deltas; the set is best-effort and depends on Unity import observation.",
                readPostconditionContract: "Asset, GUID path, schema, scene, prefab, ProjectSettings, and readIndex surfaces may be stale after refresh/import.",
                failureSemantics: "AssetDatabase refresh is not transactional; timeout, cancellation, domain reload, or import failure can leave partially imported or indeterminate project state.",
                dangerousNotes: new[] { "This operation can run Unity import code and persist importer side effects outside the requested operation set." }));

        /// <summary> Executes validate phase for <c>ucli.project.refresh</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.project.refresh</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes call phase for <c>ucli.project.refresh</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            var callbackTouched = ProjectOperationUtilities.CreateTouchedResources(callbackPaths, System.Array.Empty<string>());
            var result = OperationPhaseStepResult.Success(
                applied: true,
                changed: deduplicatedTouched.Count != 0,
                touched: deduplicatedTouched);
            if (deduplicatedTouched.Count != 0)
            {
                result = result.WithPersistence();
            }

            return Task.FromResult(
                result.WithReadInvalidations(
                    deduplicatedTouched.Count == 0
                        ? null
                        : OperationReadInvalidationUtilities.CreateForProjectRefresh(callbackTouched, deduplicatedTouched)));
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

    }
}
