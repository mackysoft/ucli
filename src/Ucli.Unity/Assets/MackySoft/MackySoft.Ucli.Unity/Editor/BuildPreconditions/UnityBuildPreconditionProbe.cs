using System;
using System.Collections.Generic;
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

            var lifecycleBefore = CreateLifecycleSnapshot(readiness.Snapshot);
            if (!readiness.IsReady)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    null,
                    readiness.Error ?? CreateInternalPreconditionError("Unity editor readiness probe failed without an error."));
            }

            if (!IsEditorModeAllowed(lifecycleBefore.EditorMode, input.AllowedEditorModes))
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    null,
                    new IpcError(
                        BuildErrorCodes.BuildRuntimePolicyViolation,
                        $"Build runtime policy does not allow Unity editor mode '{lifecycleBefore.EditorMode}'.",
                        null));
            }

            var targetSupport = targetSupportProbe.Probe(input.UnityBuildTarget);
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
                        $"Unity build target literal is invalid: {input.UnityBuildTarget}.",
                        null));
            }

            if (!targetSupport.IsSupported)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    null,
                    CreateInputProbe(input, targetSupport, Array.Empty<string>(), BuildOptions.None),
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
                    CreateInputProbe(input, targetSupport, Array.Empty<string>(), BuildOptions.None),
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

            if (ContractLiteralCodec.Matches(dirtyState.Coverage, IpcBuildDirtyStateCoverage.Partial))
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
        public IpcBuildLifecycleSnapshot CaptureAfterBuild ()
        {
            return CreateLifecycleSnapshot(readinessGate.CaptureSnapshot());
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
            var itemsByPath = new Dictionary<string, IpcBuildDirtyStateItem>(StringComparer.Ordinal);
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

                var normalizedPath = NormalizeLoadedScenePath(scene.path);
                AddDirtyItem(
                    itemsByPath,
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    normalizedPath);
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
            items.Sort(static (left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
            return new IpcBuildDirtyState(
                Checked: true,
                Dirty: items.Count != 0,
                Coverage: ContractLiteralCodec.ToValue(coverage),
                Items: items);
        }

        private static void CapturePersistentDirtyObjects (
            Dictionary<string, IpcBuildDirtyStateItem> itemsByPath,
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

                var path = NormalizeLoadedScenePath(AssetDatabase.GetAssetPath(target));
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (!IsPersistentDirtyObjectAuditedPath(path))
                {
                    continue;
                }

                AddDirtyItem(itemsByPath, ContractLiteralCodec.ToValue(ClassifyDirtyItem(path)), path);
            }
        }

        private static void AddDirtyItem (
            Dictionary<string, IpcBuildDirtyStateItem> itemsByPath,
            string kind,
            string path)
        {
            if (!itemsByPath.ContainsKey(path))
            {
                itemsByPath[path] = new IpcBuildDirtyStateItem(kind, path);
            }
        }

        internal static bool IsPersistentDirtyObjectAuditedPath (string path)
        {
            if (!UnityProjectMutationAuditScope.IsAuditedProjectPath(path))
            {
                return false;
            }

            if (!path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return true;
            }

            return HasProjectLocalPackagePath(path);
        }

        private static bool HasProjectLocalPackagePath (string path)
        {
            var projectRootPathResult = PathNormalizer.TryNormalizeFullPath(Path.Combine(Application.dataPath, ".."));
            if (!projectRootPathResult.IsSuccess)
            {
                return false;
            }

            var projectRootPath = projectRootPathResult.FullPath!;
            var packageRootPath = Path.Combine(projectRootPath, "Packages");
            var packagePathResult = RepositoryPathNormalizer.TryNormalize(projectRootPath, path);
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

        private static IpcBuildDirtyStateItemKind ClassifyDirtyItem (string path)
        {
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return IpcBuildDirtyStateItemKind.Scene;
            }

            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return IpcBuildDirtyStateItemKind.Prefab;
            }

            if (path.StartsWith("ProjectSettings/", StringComparison.Ordinal))
            {
                return IpcBuildDirtyStateItemKind.ProjectSettings;
            }

            if (path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return IpcBuildDirtyStateItemKind.Asset;
            }

            return IpcBuildDirtyStateItemKind.Unknown;
        }

        private static bool IsEditorModeAllowed (
            string? editorMode,
            IReadOnlyList<string> allowedEditorModes)
        {
            if (allowedEditorModes == null || allowedEditorModes.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(editorMode))
            {
                return false;
            }

            if (!ContractLiteralCodec.TryParse<DaemonEditorMode>(editorMode, out var resolvedEditorMode))
            {
                return false;
            }

            for (var i = 0; i < allowedEditorModes.Count; i++)
            {
                if (ContractLiteralCodec.Matches(allowedEditorModes[i], resolvedEditorMode))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveScenePaths (
            UnityBuildPreconditionInput input,
            CancellationToken cancellationToken,
            out string[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ContractLiteralCodec.TryParse<BuildProfileSceneSource>(input.SceneSource, out var sceneSource))
            {
                scenePaths = Array.Empty<string>();
                error = new IpcError(
                    BuildErrorCodes.BuildInputsInvalid,
                    $"Build scene source is invalid: {input.SceneSource}.",
                    null);
                return false;
            }

            if (sceneSource == BuildProfileSceneSource.Explicit
                || sceneSource == BuildProfileSceneSource.UnityBuildProfile)
            {
                return TryResolveExplicitScenePaths(input.ScenePaths, cancellationToken, out scenePaths, out error);
            }

            return TryResolveEditorBuildSettingsScenePaths(cancellationToken, out scenePaths, out error);
        }

        private static bool TryResolveExplicitScenePaths (
            IReadOnlyList<string> paths,
            CancellationToken cancellationToken,
            out string[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (paths == null || paths.Count == 0)
            {
                scenePaths = Array.Empty<string>();
                error = new IpcError(
                    BuildErrorCodes.BuildInputsInvalid,
                    "Explicit build scene input must contain at least one scene path.",
                    null);
                return false;
            }

            return TryValidateScenePaths(paths, cancellationToken, out scenePaths, out error);
        }

        private static bool TryResolveEditorBuildSettingsScenePaths (
            CancellationToken cancellationToken,
            out string[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enabledScenes = new List<string>();
            var editorBuildSettingsScenes = EditorBuildSettings.scenes;
            for (var i = 0; i < editorBuildSettingsScenes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scene = editorBuildSettingsScenes[i];
                if (!scene.enabled)
                {
                    continue;
                }

                enabledScenes.Add(scene.path);
            }

            if (enabledScenes.Count == 0)
            {
                scenePaths = Array.Empty<string>();
                error = new IpcError(
                    BuildErrorCodes.BuildInputsInvalid,
                    "Editor Build Settings must contain at least one enabled scene.",
                    null);
                return false;
            }

            return TryValidateScenePaths(enabledScenes, cancellationToken, out scenePaths, out error);
        }

        private static bool TryValidateScenePaths (
            IReadOnlyList<string> paths,
            CancellationToken cancellationToken,
            out string[] scenePaths,
            out IpcError? error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scenePaths = new string[paths.Count];
            for (var i = 0; i < paths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawPath = paths[i];
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    scenePaths = Array.Empty<string>();
                    error = new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Build scene path at index {i} must not be empty.",
                        null);
                    return false;
                }

                if (!UnityAssetPathContract.IsNormalizedSceneAssetPath(rawPath))
                {
                    scenePaths = Array.Empty<string>();
                    error = new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Build scene path at index {i} must be a normalized project-relative scene path under Assets ending with '{UnityAssetPathContract.SceneAssetExtension}': {rawPath}.",
                        null);
                    return false;
                }

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(rawPath);
                if (sceneAsset == null)
                {
                    scenePaths = Array.Empty<string>();
                    error = new IpcError(
                        BuildErrorCodes.BuildSceneNotFound,
                        $"Build scene path at index {i} does not resolve to a scene asset: {rawPath}.",
                        null);
                    return false;
                }

                var canonicalPath = AssetDatabase.GetAssetPath(sceneAsset);
                if (!string.Equals(rawPath, canonicalPath, StringComparison.Ordinal))
                {
                    scenePaths = Array.Empty<string>();
                    error = new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Build scene path at index {i} must match the scene asset's canonical project path: {rawPath}.",
                        null);
                    return false;
                }

                scenePaths[i] = canonicalPath;
            }

            error = null;
            return true;
        }

        private static string NormalizeLoadedScenePath (string path)
        {
            return path.Trim().Replace('\\', '/');
        }

        private IpcBuildInputProbe CreateInputProbe (
            UnityBuildPreconditionInput input,
            UnityBuildTargetSupportProbeResult targetSupport,
            IReadOnlyList<string> scenePaths,
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

        private IpcBuildLifecycleSnapshot CreateLifecycleSnapshot (UnityEditorLifecycleSnapshot snapshot)
        {
            return UnityLifecycleResponseCodec.CreateBuildLifecycleSnapshot(
                projectIdentity.UnityVersion,
                serverVersionProvider.GetVersion(),
                projectIdentity.ProjectFingerprint,
                snapshot);
        }

        private static IpcError CreateInternalPreconditionError (string message)
        {
            return new IpcError(UcliCoreErrorCodes.InternalError, message, null);
        }
    }
}
