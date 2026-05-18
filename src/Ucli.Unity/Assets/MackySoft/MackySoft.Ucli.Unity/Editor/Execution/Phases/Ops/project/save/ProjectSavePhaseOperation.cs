using System;
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
    internal sealed class ProjectSavePhaseOperation : UcliOperation<UcliEmptyArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.ProjectSave,
            kind: UcliOperationKind.Mutation,
            description: "Saves dirty project assets, scenes, and prefab stages known to uCLI.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[]
                {
                    UcliOperationSideEffect.SceneSave,
                    UcliOperationSideEffect.PrefabSave,
                    UcliOperationSideEffect.AssetSave,
                    UcliOperationSideEffect.ProjectSave,
                },
                touchedKinds: new[]
                {
                    IpcExecuteTouchedResourceKindNames.Scene,
                    IpcExecuteTouchedResourceKindNames.Prefab,
                    IpcExecuteTouchedResourceKindNames.Asset,
                    IpcExecuteTouchedResourceKindNames.ProjectSettings,
                },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Observe request-attributed dirty resources that a project save would persist without writing project files.",
                callSemantics: "Persist request-attributed dirty scenes, prefab stages, assets, and ProjectSettings through Unity save APIs.",
                touchedContract: "Reports the scene, prefab, asset, and ProjectSettings resources known to be request-attributed and save-relevant.",
                readPostconditionContract: "Asset, scene, prefab, ProjectSettings, GUID path, and readIndex surfaces covering saved resources may be stale after a successful call.",
                failureSemantics: "Project save is not transactional; timeout, cancellation, or Unity failure can leave partial or indeterminate file changes across saved resource kinds.",
                dangerousNotes: new[] { "This operation can persist multiple project resource kinds in one save boundary without transactional rollback." }));

        /// <summary> Executes validate phase for <c>ucli.project.save</c>. </summary>
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

        /// <summary> Executes plan phase for <c>ucli.project.save</c>. </summary>
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
        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var touched = new List<OperationTouch>();
            var observedPersistence = false;
            // NOTE:
            // Unity project saves are not transactional across scenes, prefab stages, and project assets.
            // When project-scoped persistence is requested, request-attributed live scene/prefab changes
            // must be flushed before File/Save Project so opened editor state stays consistent with the
            // serialized project data that follows.
            if (!TrySaveRequestAttributedLoadedScenes(operation, executionContext, touched, ref observedPersistence, out var sceneFailure))
            {
                return Task.FromResult(WithObservedProjectSaveEvidence(sceneFailure!, touched, observedPersistence));
            }

            if (!TrySaveRequestAttributedOpenedPrefabStage(operation, executionContext, touched, ref observedPersistence, out var prefabFailure))
            {
                return Task.FromResult(WithObservedProjectSaveEvidence(prefabFailure!, touched, observedPersistence));
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
                var failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Project could not be saved.");
                return Task.FromResult(WithObservedProjectSaveEvidence(failure, touched, observedPersistence));
            }

            var afterSnapshot = ProjectOperationUtilities.CaptureProjectSettingsSnapshot(projectRoot);
            var changedProjectSettingsPaths = ProjectOperationUtilities.GetChangedProjectSettingsPaths(beforeSnapshot, afterSnapshot);
            var projectTouched = ProjectOperationUtilities.CreateTouchedResources(callbackPaths, changedProjectSettingsPaths);
            for (var i = 0; i < projectTouched.Count; i++)
            {
                touched.Add(projectTouched[i]);
                executionContext.UnmarkRequestAttributedChange(new OperationResource(projectTouched[i].Kind, projectTouched[i].Path));
            }

            var result = OperationPhaseStepResult.Success(
                applied: true,
                changed: touched.Count != 0,
                touched: touched);
            if (touched.Count != 0)
            {
                result = result.WithPersistence();
            }

            return Task.FromResult(
                result.WithReadInvalidations(touched.Count == 0 ? null : OperationReadInvalidationUtilities.CreateForProjectSave(touched)));
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

        /// <summary> Returns a failure result carrying project-save evidence already observed before the failure. </summary>
        /// <param name="failure"> The failed project-save result. </param>
        /// <param name="touched"> The project resources already saved or observed before the failure. </param>
        /// <param name="observedPersistence"> Whether a save API succeeded before the failure. </param>
        /// <returns> The failure result with observed project-save evidence attached. </returns>
        internal static OperationPhaseStepResult WithObservedProjectSaveEvidence (
            OperationPhaseStepResult failure,
            IReadOnlyList<OperationTouch> touched,
            bool observedPersistence)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            if (touched == null)
            {
                throw new ArgumentNullException(nameof(touched));
            }

            if (failure.IsSuccess || touched.Count == 0 || !observedPersistence)
            {
                return failure;
            }

            return OperationPhaseStepResult.Failed(
                    failure.Failure!,
                    applied: true,
                    changed: true,
                    touched: touched,
                    result: failure.Result)
                .WithDiagnostics(failure.Diagnostics)
                .WithPersistence()
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateForProjectSave(touched));
        }

        private static bool TrySaveRequestAttributedLoadedScenes (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            ICollection<OperationTouch> touched,
            ref bool observedPersistence,
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

                if (scene.isDirty)
                {
                    if (!EditorSceneManager.SaveScene(scene, scene.path))
                    {
                        failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                            operation.Id,
                            $"Scene could not be saved: {scene.path}.");
                        return false;
                    }

                    observedPersistence = true;
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
            ref bool observedPersistence,
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

                observedPersistence = true;
                prefabStage.ClearDirtiness();
            }

            executionContext.UnmarkRequestAttributedChange(resource);
            touched.Add(OperationResourceUtilities.CreateTouch(resource));
            return true;
        }
    }
}
