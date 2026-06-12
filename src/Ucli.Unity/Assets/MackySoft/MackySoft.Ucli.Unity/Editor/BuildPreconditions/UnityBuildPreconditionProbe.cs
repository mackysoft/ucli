using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Performs BuildPipeline precondition checks and lifecycle capture for build execution. </summary>
    internal sealed class UnityBuildPreconditionProbe
    {
        private const string ExplicitSceneSource = "explicit";
        private const string EditorBuildSettingsSceneSource = "editorBuildSettings";
        private const string AssetsRootPrefix = "Assets/";
        private const string SceneAssetExtension = ".unity";

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

            var targetSupport = targetSupportProbe.Probe(input.UnityBuildTarget);
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
                        $"Unity build target module is not installed or not supported: {targetSupport.Target}.",
                        null));
            }

            if (!TryResolveScenePaths(input, out var scenePaths, out var inputError))
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
            var dirtyState = CaptureDirtyState(scenePaths);
            if (dirtyState.Dirty)
            {
                return UnityBuildPreconditionProbeResult.Failure(
                    projectIdentity,
                    lifecycleBefore,
                    dirtyState,
                    resolvedInputProbe,
                    new IpcError(
                        BuildErrorCodes.BuildDirtyStatePresent,
                        "One or more build input scenes have unsaved changes.",
                        null));
            }

            return UnityBuildPreconditionProbeResult.Success(
                projectIdentity,
                lifecycleBefore,
                dirtyState,
                resolvedInputProbe,
                new UnityBuildResolvedInput(
                    targetSupport.Target,
                    targetSupport.TargetGroup,
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

        private static IpcBuildDirtyState CaptureDirtyState (IReadOnlyList<string> scenePaths)
        {
            var buildInputScenePaths = new HashSet<string>(scenePaths, StringComparer.Ordinal);
            var items = new List<IpcBuildDirtyStateItem>();
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid()
                    || !scene.isLoaded
                    || string.IsNullOrWhiteSpace(scene.path)
                    || EditorSceneManager.IsPreviewScene(scene)
                    || !scene.isDirty)
                {
                    continue;
                }

                var normalizedPath = NormalizeProjectPath(scene.path);
                if (!buildInputScenePaths.Contains(normalizedPath))
                {
                    continue;
                }

                items.Add(new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItemKindNames.Scene,
                    normalizedPath));
            }

            items.Sort(static (left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
            return new IpcBuildDirtyState(
                Checked: true,
                Dirty: items.Count != 0,
                Items: items);
        }

        private static bool TryResolveScenePaths (
            UnityBuildPreconditionInput input,
            out string[] scenePaths,
            out IpcError? error)
        {
            if (string.Equals(input.SceneSource, ExplicitSceneSource, StringComparison.Ordinal))
            {
                return TryResolveExplicitScenePaths(input.ScenePaths, out scenePaths, out error);
            }

            if (string.Equals(input.SceneSource, EditorBuildSettingsSceneSource, StringComparison.Ordinal))
            {
                return TryResolveEditorBuildSettingsScenePaths(out scenePaths, out error);
            }

            scenePaths = Array.Empty<string>();
            error = new IpcError(
                BuildErrorCodes.BuildInputsInvalid,
                $"Build scene source is invalid: {input.SceneSource}.",
                null);
            return false;
        }

        private static bool TryResolveExplicitScenePaths (
            IReadOnlyList<string> paths,
            out string[] scenePaths,
            out IpcError? error)
        {
            if (paths == null || paths.Count == 0)
            {
                scenePaths = Array.Empty<string>();
                error = new IpcError(
                    BuildErrorCodes.BuildInputsInvalid,
                    "Explicit build scene input must contain at least one scene path.",
                    null);
                return false;
            }

            return TryValidateScenePaths(paths, out scenePaths, out error);
        }

        private static bool TryResolveEditorBuildSettingsScenePaths (
            out string[] scenePaths,
            out IpcError? error)
        {
            var enabledScenes = new List<string>();
            var editorBuildSettingsScenes = EditorBuildSettings.scenes;
            for (var i = 0; i < editorBuildSettingsScenes.Length; i++)
            {
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

            return TryValidateScenePaths(enabledScenes, out scenePaths, out error);
        }

        private static bool TryValidateScenePaths (
            IReadOnlyList<string> paths,
            out string[] scenePaths,
            out IpcError? error)
        {
            scenePaths = new string[paths.Count];
            for (var i = 0; i < paths.Count; i++)
            {
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

                var path = NormalizeProjectPath(rawPath);
                if (!IsProjectRelativeSceneAssetPath(path))
                {
                    scenePaths = Array.Empty<string>();
                    error = new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Build scene path at index {i} must be under Assets and end with '{SceneAssetExtension}': {path}.",
                        null);
                    return false;
                }

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (sceneAsset == null)
                {
                    scenePaths = Array.Empty<string>();
                    error = new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"Build scene path at index {i} does not resolve to a scene asset: {path}.",
                        null);
                    return false;
                }

                scenePaths[i] = path;
            }

            error = null;
            return true;
        }

        private static bool IsProjectRelativeSceneAssetPath (string path)
        {
            return path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal)
                && path.EndsWith(SceneAssetExtension, StringComparison.Ordinal)
                && !path.Contains("//", StringComparison.Ordinal)
                && !path.Contains('\\');
        }

        private static string NormalizeProjectPath (string path)
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
                TargetStableName: input.TargetStableName,
                UnityBuildTarget: targetSupport.Target.ToString(),
                UnityBuildTargetGroup: targetSupport.TargetGroup.ToString(),
                SceneSource: input.SceneSource,
                Scenes: scenePaths,
                BuildOptions: buildOptions.ToString());
        }

        private IpcBuildLifecycleSnapshot CreateLifecycleSnapshot (UnityEditorLifecycleSnapshot snapshot)
        {
            return new IpcBuildLifecycleSnapshot(
                ServerVersion: serverVersionProvider.GetVersion(),
                EditorMode: ContractLiteralCodec.ToValue(snapshot.EditorMode),
                UnityVersion: projectIdentity.UnityVersion,
                ProjectFingerprint: projectIdentity.ProjectFingerprint,
                LifecycleState: snapshot.LifecycleState,
                BlockingReason: snapshot.BlockingReason,
                CompileState: snapshot.CompileState,
                CompileGeneration: snapshot.CompileGeneration,
                DomainReloadGeneration: snapshot.DomainReloadGeneration,
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
                ObservedAtUtc: snapshot.ObservedAtUtc,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic,
                PlayMode: snapshot.PlayMode);
        }

        private static IpcError CreateInternalPreconditionError (string message)
        {
            return new IpcError(UcliCoreErrorCodes.InternalError, message, null);
        }
    }
}
