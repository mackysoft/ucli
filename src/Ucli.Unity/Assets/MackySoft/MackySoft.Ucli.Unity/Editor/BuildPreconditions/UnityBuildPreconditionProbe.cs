using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Performs BuildPipeline precondition checks and lifecycle capture for build execution. </summary>
    internal sealed class UnityBuildPreconditionProbe
    {
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IpcProjectIdentity projectIdentity;
        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityBuildTargetSupportProbe targetSupportProbe;

        /// <summary> Initializes a new instance of the <see cref="UnityBuildPreconditionProbe" /> class. </summary>
        public UnityBuildPreconditionProbe (
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IServerVersionProvider serverVersionProvider,
            IUnityBuildTargetSupportProbe targetSupportProbe)
        {
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.targetSupportProbe = targetSupportProbe ?? throw new ArgumentNullException(nameof(targetSupportProbe));
        }

        /// <summary> Probes build readiness and resolves the Unity BuildPipeline input that #383 will execute. </summary>
        /// <param name="input"> The resolved build precondition input. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> The precondition result, including lifecycle before and resolved BuildPipeline input on success. </returns>
        public async Task<UnityBuildPreconditionProbeResult> ProbeBeforeBuildAsync (
            UnityBuildPreconditionInput input,
            CancellationToken cancellationToken = default)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var readiness = await readinessGate.EnsureExecutionReadyAsync(
                failFast: false,
                cancellationToken: cancellationToken,
                allowPlayMode: false);
            cancellationToken.ThrowIfCancellationRequested();

            var lifecycleBefore = CreateObservation(readiness.Observation);
            if (!readiness.IsReady)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    null,
                    readiness.Error ?? CreateInternalPreconditionError("Unity editor readiness probe failed without an error."));
            }

            if (!IsEditorModeAllowed(lifecycleBefore.State.EditorMode, input.AllowedEditorModes))
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    null,
                    new IpcError(
                        BuildErrorCodes.BuildRuntimePolicyViolation,
                        $"Build runtime policy does not allow Unity editor mode '{lifecycleBefore.State.EditorMode}'.",
                        null));
            }

            var targetSupport = targetSupportProbe.Probe(input.BuildTarget);
            cancellationToken.ThrowIfCancellationRequested();
            if (!targetSupport.IsValidTarget)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    null,
                    new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Unity build target is invalid: {ContractLiteralCodec.ToValue(input.BuildTarget)}.",
                        null));
            }

            if (!targetSupport.IsSupported)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    CreateInputProbe(input, targetSupport, Array.Empty<SceneAssetPath>(), BuildOptions.None),
                    new IpcError(
                        BuildErrorCodes.BuildTargetModuleMissing,
                        $"Unity buildTarget module is not installed or not supported: {targetSupport.UnityBuildTarget}.",
                        null));
            }

            if (!TryResolveScenePaths(input, cancellationToken, out var scenePaths, out var inputError))
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    CreateInputProbe(input, targetSupport, Array.Empty<SceneAssetPath>(), BuildOptions.None),
                    inputError!);
            }

            var buildOptions = CreateBuildOptions(input);
            var resolvedInputProbe = CreateInputProbe(input, targetSupport, scenePaths, buildOptions);
            var dirtyState = CaptureDirtyState(cancellationToken);
            if (dirtyState.Dirty)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    dirtyState,
                    resolvedInputProbe,
                    new IpcError(
                        BuildErrorCodes.BuildDirtyStatePresent,
                        "One or more project items have unsaved changes.",
                        null));
            }

            if (dirtyState.Coverage == IpcBuildDirtyStateCoverage.Partial)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    dirtyState,
                    resolvedInputProbe,
                    new IpcError(
                        BuildErrorCodes.BuildDirtyStateIndeterminate,
                        "Build dirty state could not be checked with full coverage.",
                        null));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return UnityBuildPreconditionProbeResult.Success(
                projectIdentity,
                lifecycleBefore,
                dirtyState,
                resolvedInputProbe,
                new UnityBuildResolvedInput(
                    targetSupport.UnityBuildTarget,
                    targetSupport.UnityBuildTargetGroup,
                    scenePaths,
                    buildOptions));
        }

        /// <summary> Captures lifecycle state after BuildPipeline completion, failure, or cancellation. </summary>
        /// <returns> The lifecycle snapshot for <c>build.json.lifecycle.after</c>. </returns>
        public IpcUnityEditorObservation CaptureAfterBuild ()
        {
            return CreateObservation(readinessGate.CaptureObservation());
        }

        private static BuildOptions CreateBuildOptions (UnityBuildPreconditionInput input)
        {
            return input.Development
                ? BuildOptions.Development
                : BuildOptions.None;
        }

        /// <summary> Captures the dirty-state snapshot for audited project items. </summary>
        public static IpcBuildDirtyState CaptureDirtyState (
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemsByPath = new SortedDictionary<ProjectMutationAuditPath, IpcBuildDirtyStateItem>();
            var coverage = IpcBuildDirtyStateCoverage.Full;
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid()
                    || !scene.isLoaded
                    || string.IsNullOrWhiteSpace(scene.path)
                    || EditorSceneManager.IsPreviewScene(scene)
                    || !scene.isDirty)
                {
                    continue;
                }

                if (!ProjectMutationAuditPath.TryParse(scene.path, out var auditPath))
                {
                    coverage = IpcBuildDirtyStateCoverage.Partial;
                    continue;
                }

                AddDirtyItem(itemsByPath, auditPath);
            }

            try
            {
                CapturePersistentDirtyObjects(itemsByPath, cancellationToken);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                coverage = IpcBuildDirtyStateCoverage.Partial;
            }

            var items = new List<IpcBuildDirtyStateItem>(itemsByPath.Values);
            return new IpcBuildDirtyState(
                Dirty: items.Count != 0,
                Coverage: coverage,
                Items: items);
        }

        private static void CapturePersistentDirtyObjects (
            SortedDictionary<ProjectMutationAuditPath, IpcBuildDirtyStateItem> itemsByPath,
            CancellationToken cancellationToken)
        {
            var objects = Resources.FindObjectsOfTypeAll<Object>();
            for (var i = 0; i < objects.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var target = objects[i];
                if (target == null
                    || !EditorUtility.IsPersistent(target)
                    || !EditorUtility.IsDirty(target))
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(target);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (!TryResolvePersistentDirtyObjectAuditPath(path, out var auditPath))
                {
                    continue;
                }

                AddDirtyItem(itemsByPath, auditPath);
            }
        }

        private static void AddDirtyItem (
            SortedDictionary<ProjectMutationAuditPath, IpcBuildDirtyStateItem> itemsByPath,
            ProjectMutationAuditPath path)
        {
            if (!itemsByPath.ContainsKey(path))
            {
                itemsByPath[path] = new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItem.ClassifyPath(path),
                    path);
            }
        }

        internal static bool TryResolvePersistentDirtyObjectAuditPath (
            string path,
            [NotNullWhen(true)] out ProjectMutationAuditPath? auditPath)
        {
            if (!ProjectMutationAuditPath.TryParse(path, out auditPath))
            {
                return false;
            }

            if (!auditPath.Value.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return true;
            }

            if (HasProjectLocalPackagePath(auditPath))
            {
                return true;
            }

            auditPath = null;
            return false;
        }

        private static bool HasProjectLocalPackagePath (ProjectMutationAuditPath path)
        {
            var projectRootPathResult = PathNormalizer.TryNormalizeFullPath(Path.Combine(Application.dataPath, ".."));
            if (!projectRootPathResult.IsSuccess)
            {
                return false;
            }

            var projectRootPath = projectRootPathResult.FullPath!;
            var packageRootPath = Path.Combine(projectRootPath, "Packages");
            var packagePathResult = RepositoryPathNormalizer.TryNormalize(projectRootPath, path.Value);
            if (!packagePathResult.IsSuccess)
            {
                return false;
            }

            var packagePath = packagePathResult.FullPath!;
            if (!RepositoryPathNormalizer.TryNormalize(packageRootPath, packagePath).IsSuccess)
            {
                return false;
            }

            return File.Exists(packagePath) || Directory.Exists(packagePath);
        }

        private static bool IsEditorModeAllowed (
            DaemonEditorMode editorMode,
            IReadOnlyList<DaemonEditorMode> allowedEditorModes)
        {
            for (var i = 0; i < allowedEditorModes.Count; i++)
            {
                if (allowedEditorModes[i] == editorMode)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveScenePaths (
            UnityBuildPreconditionInput input,
            CancellationToken cancellationToken,
            out SceneAssetPath[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.SceneSource == BuildProfileSceneSource.Explicit
                || input.SceneSource == BuildProfileSceneSource.UnityBuildProfile)
            {
                return TryResolveExplicitScenePaths(input.ScenePaths, cancellationToken, out scenePaths, out error);
            }

            return TryResolveEditorBuildSettingsScenePaths(cancellationToken, out scenePaths, out error);
        }

        private static bool TryResolveExplicitScenePaths (
            IReadOnlyList<SceneAssetPath> paths,
            CancellationToken cancellationToken,
            out SceneAssetPath[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (paths.Count == 0)
            {
                scenePaths = Array.Empty<SceneAssetPath>();
                error = new IpcError(
                    BuildErrorCodes.BuildInputsInvalid,
                    "Explicit build scene input must contain at least one scene path.",
                    null);
                return false;
            }

            return TryResolveSceneAssets(paths, cancellationToken, out scenePaths, out error);
        }

        private static bool TryResolveEditorBuildSettingsScenePaths (
            CancellationToken cancellationToken,
            out SceneAssetPath[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enabledScenes = new List<SceneAssetPath>();
            var editorBuildSettingsScenes = EditorBuildSettings.scenes;
            for (var i = 0; i < editorBuildSettingsScenes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scene = editorBuildSettingsScenes[i];
                if (!scene.enabled)
                {
                    continue;
                }

                if (!SceneAssetPath.TryParse(scene.path, out var scenePath))
                {
                    scenePaths = Array.Empty<SceneAssetPath>();
                    error = new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Editor Build Settings scene path is invalid at index {i}: {scene.path}.",
                        null);
                    return false;
                }

                enabledScenes.Add(scenePath);
            }

            if (enabledScenes.Count == 0)
            {
                scenePaths = Array.Empty<SceneAssetPath>();
                error = new IpcError(
                    BuildErrorCodes.BuildInputsInvalid,
                    "Editor Build Settings must contain at least one enabled scene.",
                    null);
                return false;
            }

            return TryResolveSceneAssets(enabledScenes, cancellationToken, out scenePaths, out error);
        }

        private static bool TryResolveSceneAssets (
            IReadOnlyList<SceneAssetPath> paths,
            CancellationToken cancellationToken,
            out SceneAssetPath[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scenePaths = new SceneAssetPath[paths.Count];
            for (var i = 0; i < paths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var path = paths[i];
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path.Value);
                if (sceneAsset == null)
                {
                    scenePaths = Array.Empty<SceneAssetPath>();
                    error = new IpcError(
                        BuildErrorCodes.BuildSceneNotFound,
                        $"Build scene path at index {i} does not resolve to a scene asset: {path.Value}.",
                        null);
                    return false;
                }

                var canonicalPath = AssetDatabase.GetAssetPath(sceneAsset);
                if (!string.Equals(path.Value, canonicalPath, StringComparison.Ordinal))
                {
                    scenePaths = Array.Empty<SceneAssetPath>();
                    error = new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Build scene path at index {i} must match the scene asset's canonical project path: {path.Value}.",
                        null);
                    return false;
                }

                scenePaths[i] = path;
            }

            error = null;
            return true;
        }

        private IpcBuildInputProbe CreateInputProbe (
            UnityBuildPreconditionInput input,
            UnityBuildTargetSupportProbeResult targetSupport,
            IReadOnlyList<SceneAssetPath> scenePaths,
            BuildOptions buildOptions)
        {
            return new IpcBuildInputProbe(
                BuildTarget: input.BuildTarget,
                UnityBuildTarget: targetSupport.UnityBuildTarget.ToString(),
                UnityBuildTargetGroup: targetSupport.UnityBuildTargetGroup.ToString(),
                InputKind: input.InputKind,
                SceneSource: input.SceneSource,
                Scenes: scenePaths,
                BuildOptions: buildOptions.ToString());
        }

        private IpcUnityEditorObservation CreateObservation (UnityEditorObservation snapshot)
        {
            return UnityLifecycleResponseFactory.Create(
                projectIdentity,
                serverVersionProvider.GetVersion(),
                snapshot);
        }

        private static IpcError CreateInternalPreconditionError (string message)
        {
            return new IpcError(UcliCoreErrorCodes.InternalError, message, null);
        }
    }
}
