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
    /// <summary> Implements <c>ucli.project.save</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class ProjectSavePhaseOperation : TypedUcliOperation<UcliEmptyArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.ProjectSave,
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced);

        /// <summary> Executes validate phase for <c>ucli.project.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.project.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var touched = CollectKnownPlannedTouchedResources(executionContext);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: touched.Count != 0,
                touched: touched));
        }

        /// <summary> Executes call phase for <c>ucli.project.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var touched = new List<OperationTouch>();
            // NOTE:
            // Unity project saves are not transactional across scenes, prefab stages, and project assets.
            // When project-scoped persistence is requested, request-attributed live scene/prefab changes
            // must be flushed before File/Save Project so opened editor state stays consistent with the
            // serialized project data that follows.
            if (!TrySaveRequestAttributedLoadedScenes(operation, executionContext, touched, out var sceneFailure))
            {
                return Task.FromResult(sceneFailure!);
            }

            if (!TrySaveRequestAttributedOpenedPrefabStage(operation, executionContext, touched, out var prefabFailure))
            {
                return Task.FromResult(prefabFailure!);
            }

            var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
            var beforeSnapshot = ProjectOperationUtilities.CaptureProjectSettingsSnapshot(projectRoot);
            var scopeId = ProjectOperationCallbackRegistry.BeginSaveCapture();
            IReadOnlyList<string> callbackPaths;
            bool executed;
            try
            {
                executed = EditorApplication.ExecuteMenuItem("File/Save Project");
            }
            finally
            {
                callbackPaths = ProjectOperationCallbackRegistry.EndSaveCapture(scopeId);
            }

            if (!executed)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Project could not be saved."));
            }

            var afterSnapshot = ProjectOperationUtilities.CaptureProjectSettingsSnapshot(projectRoot);
            var changedProjectSettingsPaths = ProjectOperationUtilities.GetChangedProjectSettingsPaths(beforeSnapshot, afterSnapshot);
            var projectTouched = ProjectOperationUtilities.CreateTouchedResources(callbackPaths, changedProjectSettingsPaths);
            for (var i = 0; i < projectTouched.Count; i++)
            {
                touched.Add(projectTouched[i]);
                executionContext.UnmarkRequestAttributedChange(new OperationResource(projectTouched[i].Kind, projectTouched[i].Path));
            }

            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: touched.Count != 0,
                    touched: touched)
                .WithReadInvalidations(touched.Count == 0 ? null : OperationReadInvalidationUtilities.CreateForProjectSave(touched)));
        }

        private static IReadOnlyList<OperationTouch> CollectKnownPlannedTouchedResources (OperationExecutionContext executionContext)
        {
            var touched = new List<OperationTouch>();
            var touchedResources = new HashSet<OperationResource>();
            var requestAttributedResources = new List<OperationResource>();
            executionContext.CopyRequestAttributedChangesTo(requestAttributedResources);

            for (var resourceIndex = 0; resourceIndex < requestAttributedResources.Count; resourceIndex++)
            {
                var resource = requestAttributedResources[resourceIndex];
                if (resource.Kind == OperationTouchKind.Scene)
                {
                    if (executionContext.HasPlannedLiveSceneOpen(resource.Path))
                    {
                        AddTouchedResource(resource, touchedResources, touched);
                    }

                    continue;
                }

                if (resource.Kind == OperationTouchKind.Prefab)
                {
                    if (executionContext.HasPlannedLivePrefabOpen(resource.Path))
                    {
                        AddTouchedResource(resource, touchedResources, touched);
                    }

                    continue;
                }

                AddTouchedResource(resource, touchedResources, touched);
            }

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid()
                    || !scene.isLoaded
                    || EditorSceneManager.IsPreviewScene(scene)
                    || string.IsNullOrWhiteSpace(scene.path))
                {
                    continue;
                }

                var resource = new OperationResource(OperationTouchKind.Scene, scene.path);
                if (executionContext.HasRequestAttributedChange(resource))
                {
                    AddTouchedResource(resource, touchedResources, touched);
                }
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null
                && prefabStage.prefabContentsRoot != null
                && !string.IsNullOrWhiteSpace(prefabStage.assetPath))
            {
                var resource = new OperationResource(OperationTouchKind.Prefab, prefabStage.assetPath);
                if (executionContext.HasRequestAttributedChange(resource))
                {
                    AddTouchedResource(resource, touchedResources, touched);
                }
            }

            return touched;
        }

        private static void AddTouchedResource (
            OperationResource resource,
            ISet<OperationResource> touchedResources,
            ICollection<OperationTouch> touched)
        {
            if (!touchedResources.Add(resource))
            {
                return;
            }

            touched.Add(OperationResourceUtilities.CreateTouch(resource));
        }

        private static bool TrySaveRequestAttributedLoadedScenes (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            ICollection<OperationTouch> touched,
            out OperationPhaseStepResult? failure)
        {
            failure = null;
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid()
                    || !scene.isLoaded
                    || EditorSceneManager.IsPreviewScene(scene)
                    || string.IsNullOrWhiteSpace(scene.path))
                {
                    continue;
                }

                var resource = new OperationResource(OperationTouchKind.Scene, scene.path);
                if (!executionContext.HasRequestAttributedChange(resource))
                {
                    continue;
                }

                if (scene.isDirty
                    && !EditorSceneManager.SaveScene(scene, scene.path))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        $"Scene could not be saved: {scene.path}.");
                    return false;
                }

                executionContext.UnmarkRequestAttributedChange(resource);
                touched.Add(OperationResourceUtilities.CreateTouch(resource));
            }

            return true;
        }

        private static bool TrySaveRequestAttributedOpenedPrefabStage (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            ICollection<OperationTouch> touched,
            out OperationPhaseStepResult? failure)
        {
            failure = null;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null
                || prefabStage.prefabContentsRoot == null
                || string.IsNullOrWhiteSpace(prefabStage.assetPath))
            {
                return true;
            }

            var resource = new OperationResource(OperationTouchKind.Prefab, prefabStage.assetPath);
            if (!executionContext.HasRequestAttributedChange(resource))
            {
                return true;
            }

            if (prefabStage.prefabContentsRoot.scene.isDirty)
            {
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
                if (savedPrefab == null)
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                        operation.Id,
                        $"Prefab could not be saved: {prefabStage.assetPath}.");
                    return false;
                }

                prefabStage.ClearDirtiness();
            }

            executionContext.UnmarkRequestAttributedChange(resource);
            touched.Add(OperationResourceUtilities.CreateTouch(resource));
            return true;
        }
    }
}
