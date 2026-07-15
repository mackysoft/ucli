#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Project;
using UnityEditor;
using UnityEditor.Build.Profile;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves Unity 6000 Build Profile asset inputs. </summary>
    internal sealed class Unity6000BuildProfileInputResolver : IUnityBuildProfileInputResolver
    {
        private readonly UnityBuildPreconditionProbe preconditionProbe;

        /// <summary> Initializes a new instance of the <see cref="Unity6000BuildProfileInputResolver" /> class. </summary>
        public Unity6000BuildProfileInputResolver (UnityBuildPreconditionProbe preconditionProbe)
        {
            this.preconditionProbe = preconditionProbe ?? throw new ArgumentNullException(nameof(preconditionProbe));
        }

        /// <inheritdoc />
        public Task<UnityBuildProfileInputResolutionResult> ResolveAsync (
            IpcBuildRunRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var lifecycleBefore = preconditionProbe.CaptureAfterBuild();
            if (request.UnityBuildProfile == null)
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    CreateInvalidProfileError("Unity Build Profile input must specify a profile asset path."),
                    null,
                    lifecycleBefore));
            }

            var profilePath = request.UnityBuildProfile.Path;

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath.Value);
            if (profile == null)
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    CreateInvalidProfileError($"Unity Build Profile asset could not be resolved: {profilePath}."),
                    new IpcUnityBuildProfileInput(
                        Path: profilePath,
                        Digest: null,
                        ApplyAudit: null),
                    lifecycleBefore));
            }

            var canonicalPath = AssetDatabase.GetAssetPath(profile);
            if (!string.Equals(profilePath.Value, canonicalPath, StringComparison.Ordinal))
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    CreateInvalidProfileError($"Unity Build Profile path must match the asset canonical path: {profilePath}."),
                    new IpcUnityBuildProfileInput(
                        Path: profilePath,
                        Digest: null,
                        ApplyAudit: null),
                    lifecycleBefore));
            }

            if (!TryResolveScenePaths(profile, out var scenePaths, out var error))
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    error!,
                    new IpcUnityBuildProfileInput(
                        Path: profilePath,
                        Digest: null,
                        ApplyAudit: null),
                    lifecycleBefore));
            }

            Sha256Digest digest;
            try
            {
                digest = ComputeAssetDigest(profilePath);
                cancellationToken.ThrowIfCancellationRequested();
                if (BuildProfile.GetActiveBuildProfile() != profile)
                {
                    BuildProfile.SetActiveBuildProfile(profile);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    CreateInvalidProfileError($"Unity Build Profile asset could not be applied: {profilePath}. {exception.Message}"),
                    new IpcUnityBuildProfileInput(
                        Path: profilePath,
                        Digest: null,
                        ApplyAudit: null),
                    lifecycleBefore));
            }

            var unityBuildProfile = CreateAppliedUnityBuildProfile(profilePath, digest, lifecycleBefore);
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            if (!UnityBuildTargetSupportProbe.TryGetStableName(activeBuildTarget, out var stableBuildTarget))
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    CreateInvalidProfileError($"Unity Build Profile build target is unsupported: {activeBuildTarget}."),
                    unityBuildProfile,
                    unityBuildProfile.ApplyAudit!.LifecycleAfter,
                    unityBuildProfile.ApplyAudit.DirtyStateAfter));
            }

            if (!TryResolveOutputLayout(request.OutputPath, stableBuildTarget, out var outputLayout))
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    new IpcError(
                        BuildErrorCodes.BuildInputsInvalid,
                        $"BuildPipeline output layout could not be resolved for build target: {stableBuildTarget}.",
                        null),
                    unityBuildProfile,
                    unityBuildProfile.ApplyAudit!.LifecycleAfter,
                    unityBuildProfile.ApplyAudit.DirtyStateAfter));
            }

            var development = EditorUserBuildSettings.development;
            var preconditionInput = new UnityBuildPreconditionInput(
                InputKind: BuildProfileInputsKind.UnityBuildProfile,
                BuildTarget: stableBuildTarget,
                SceneSource: BuildProfileSceneSource.UnityBuildProfile,
                ScenePaths: scenePaths,
                Development: development,
                AllowedEditorModes: request.AllowedEditorModes);
            return Task.FromResult(UnityBuildProfileInputResolutionResult.Success(
                preconditionInput,
                outputLayout!,
                unityBuildProfile));
        }

        private static bool TryResolveScenePaths (
            BuildProfile profile,
            out SceneAssetPath[] scenePaths,
            out IpcError? error)
        {
            if (profile.overrideGlobalScenes && profile.scenes != null)
            {
                for (var i = 0; i < profile.scenes.Length; i++)
                {
                    var scene = profile.scenes[i];
                    if (!scene.enabled)
                    {
                        scenePaths = Array.Empty<SceneAssetPath>();
                        error = new IpcError(
                            BuildErrorCodes.BuildSceneDisabled,
                            $"Unity Build Profile scene at index {i} is disabled: {scene.path}.",
                            null);
                        return false;
                    }
                }
            }

            var scenesForBuild = profile.GetScenesForBuild();
            var paths = new List<SceneAssetPath>(scenesForBuild.Length);
            for (var i = 0; i < scenesForBuild.Length; i++)
            {
                var scene = scenesForBuild[i];
                if (!scene.enabled)
                {
                    scenePaths = Array.Empty<SceneAssetPath>();
                    error = new IpcError(
                        BuildErrorCodes.BuildSceneDisabled,
                        $"Unity Build Profile scene at index {i} is disabled: {scene.path}.",
                        null);
                    return false;
                }

                if (!SceneAssetPath.TryParse(scene.path, out var scenePath))
                {
                    scenePaths = Array.Empty<SceneAssetPath>();
                    error = CreateInvalidProfileError($"Unity Build Profile scene path is invalid at index {i}: {scene.path}.");
                    return false;
                }

                paths.Add(scenePath);
            }

            if (paths.Count == 0)
            {
                scenePaths = Array.Empty<SceneAssetPath>();
                error = new IpcError(
                    BuildErrorCodes.BuildInputsInvalid,
                    "Unity Build Profile must resolve at least one enabled scene.",
                    null);
                return false;
            }

            scenePaths = paths.ToArray();
            error = null;
            return true;
        }

        private static bool TryResolveOutputLayout (
            string outputPath,
            BuildTargetStableName stableBuildTarget,
            out IpcBuildOutputLayout? outputLayout)
        {
            var androidAppBundle = stableBuildTarget == BuildTargetStableName.Android
                && EditorUserBuildSettings.buildAppBundle;
            return IpcBuildOutputLayoutResolver.TryResolve(
                outputPath,
                stableBuildTarget,
                androidAppBundle,
                out outputLayout);
        }

        private IpcUnityBuildProfileInput CreateAppliedUnityBuildProfile (
            UnityBuildProfileAssetPath profilePath,
            Sha256Digest digest,
            IpcUnityEditorObservation lifecycleBefore)
        {
            // NOTE: After SetActiveBuildProfile succeeds, the editor has already been mutated.
            // Capture audit evidence without observing cancellation so post-apply failures still report
            // the lifecycle and dirty state that build.json would otherwise use as its baseline.
            var lifecycleAfter = preconditionProbe.CaptureAfterBuild();
            var dirtyStateAfter = UnityBuildPreconditionProbe.CaptureDirtyState(CancellationToken.None);
            var applyAudit = new IpcUnityBuildProfileApplyAudit(
                Applied: true,
                LifecycleBefore: lifecycleBefore,
                LifecycleAfter: lifecycleAfter,
                DirtyStateAfter: dirtyStateAfter);
            return new IpcUnityBuildProfileInput(
                Path: profilePath,
                Digest: digest,
                ApplyAudit: applyAudit);
        }

        private static Sha256Digest ComputeAssetDigest (UnityBuildProfileAssetPath profilePath)
        {
            var absolutePath = UnityAssetPathUtility.ToAbsolutePath(profilePath.Value);
            return Sha256Digest.Compute(File.ReadAllBytes(absolutePath));
        }

        private static IpcError CreateInvalidProfileError (string message)
        {
            return new IpcError(BuildErrorCodes.BuildUnityBuildProfileInvalid, message, null);
        }
    }
}
#endif
