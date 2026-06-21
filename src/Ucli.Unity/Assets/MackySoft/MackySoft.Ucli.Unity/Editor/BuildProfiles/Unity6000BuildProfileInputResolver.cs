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

            if (!TryValidateProfilePath(request.UnityBuildProfile.Path, out var profilePath, out var error))
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    error!,
                    request.UnityBuildProfile,
                    lifecycleBefore));
            }

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            if (profile == null)
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    CreateInvalidProfileError($"Unity Build Profile asset could not be resolved: {profilePath}."),
                    new IpcUnityBuildProfileInput(profilePath),
                    lifecycleBefore));
            }

            var canonicalPath = AssetDatabase.GetAssetPath(profile);
            if (!string.Equals(profilePath, canonicalPath, StringComparison.Ordinal))
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    CreateInvalidProfileError($"Unity Build Profile path must match the asset canonical path: {profilePath}."),
                    new IpcUnityBuildProfileInput(profilePath),
                    lifecycleBefore));
            }

            if (!TryResolveScenePaths(profile, out var scenePaths, out error))
            {
                return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                    error!,
                    new IpcUnityBuildProfileInput(profilePath),
                    lifecycleBefore));
            }

            string digest;
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
                    new IpcUnityBuildProfileInput(profilePath),
                    lifecycleBefore));
            }

            var unityBuildProfile = CreateAppliedUnityBuildProfile(profilePath, digest, lifecycleBefore);
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            if (!TryResolveBuildTarget(activeBuildTarget, out var stableBuildTarget, out var unityBuildTargetLiteral))
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
                InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                BuildTarget: stableBuildTarget,
                UnityBuildTarget: unityBuildTargetLiteral,
                SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                ScenePaths: scenePaths,
                Development: development,
                AllowedEditorModes: request.AllowedEditorModes);
            return Task.FromResult(UnityBuildProfileInputResolutionResult.Success(
                preconditionInput,
                outputLayout!,
                unityBuildProfile));
        }

        private static bool TryValidateProfilePath (
            string? path,
            out string profilePath,
            out IpcError? error)
        {
            profilePath = string.Empty;
            if (!UnityAssetPathContract.TryNormalizeBuildProfileAssetPath(path, out var normalizedPath)
                || !string.Equals(path, normalizedPath, StringComparison.Ordinal))
            {
                error = CreateInvalidProfileError("Unity Build Profile path must be a normalized project-relative asset path under Assets and must not reference a .meta file.");
                return false;
            }

            profilePath = normalizedPath;
            error = null;
            return true;
        }

        private static bool TryResolveBuildTarget (
            BuildTarget target,
            out string stableBuildTarget,
            out string unityBuildTargetLiteral)
        {
            unityBuildTargetLiteral = target.ToString();
            foreach (BuildTargetStableName stableName in Enum.GetValues(typeof(BuildTargetStableName)))
            {
                if (!BuildTargetStableNameUnityBuildTargetResolver.TryResolve(stableName, out var candidateUnityBuildTargetLiteral)
                    || !string.Equals(candidateUnityBuildTargetLiteral, unityBuildTargetLiteral, StringComparison.Ordinal))
                {
                    continue;
                }

                stableBuildTarget = ContractLiteralCodec.ToValue(stableName);
                return true;
            }

            stableBuildTarget = string.Empty;
            return false;
        }

        private static bool TryResolveScenePaths (
            BuildProfile profile,
            out string[] scenePaths,
            out IpcError? error)
        {
            if (profile.overrideGlobalScenes && profile.scenes != null)
            {
                for (var i = 0; i < profile.scenes.Length; i++)
                {
                    var scene = profile.scenes[i];
                    if (!scene.enabled)
                    {
                        scenePaths = Array.Empty<string>();
                        error = new IpcError(
                            BuildErrorCodes.BuildSceneDisabled,
                            $"Unity Build Profile scene at index {i} is disabled: {scene.path}.",
                            null);
                        return false;
                    }
                }
            }

            var scenesForBuild = profile.GetScenesForBuild();
            var paths = new List<string>(scenesForBuild.Length);
            for (var i = 0; i < scenesForBuild.Length; i++)
            {
                var scene = scenesForBuild[i];
                if (!scene.enabled)
                {
                    scenePaths = Array.Empty<string>();
                    error = new IpcError(
                        BuildErrorCodes.BuildSceneDisabled,
                        $"Unity Build Profile scene at index {i} is disabled: {scene.path}.",
                        null);
                    return false;
                }

                if (!UnityAssetPathContract.IsNormalizedSceneAssetPath(scene.path))
                {
                    scenePaths = Array.Empty<string>();
                    error = CreateInvalidProfileError($"Unity Build Profile scene path is invalid at index {i}: {scene.path}.");
                    return false;
                }

                paths.Add(scene.path);
            }

            if (paths.Count == 0)
            {
                scenePaths = Array.Empty<string>();
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
            string stableBuildTarget,
            out IpcBuildOutputLayout? outputLayout)
        {
            var androidAppBundle = string.Equals(stableBuildTarget, ContractLiteralCodec.ToValue(BuildTargetStableName.Android), StringComparison.Ordinal)
                && EditorUserBuildSettings.buildAppBundle;
            return IpcBuildOutputLayoutResolver.TryResolve(
                outputPath,
                stableBuildTarget,
                androidAppBundle,
                out outputLayout);
        }

        private IpcUnityBuildProfileInput CreateAppliedUnityBuildProfile (
            string profilePath,
            string digest,
            IpcBuildLifecycleSnapshot lifecycleBefore)
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
                GenerationsBefore: CreateGenerationSnapshot(lifecycleBefore),
                GenerationsAfter: CreateGenerationSnapshot(lifecycleAfter),
                DirtyStateAfter: dirtyStateAfter);
            return new IpcUnityBuildProfileInput(
                Path: profilePath,
                Digest: digest,
                ApplyAudit: applyAudit);
        }

        private static string ComputeAssetDigest (string profilePath)
        {
            var absolutePath = UnityAssetPathUtility.ToAbsolutePath(profilePath);
            return Sha256LowerHex.Compute(File.ReadAllBytes(absolutePath));
        }

        private static IpcBuildGenerationSnapshot CreateGenerationSnapshot (IpcBuildLifecycleSnapshot lifecycle)
        {
            return new IpcBuildGenerationSnapshot(
                lifecycle.CompileGeneration,
                lifecycle.DomainReloadGeneration,
                lifecycle.AssetRefreshGeneration);
        }

        private static IpcError CreateInvalidProfileError (string message)
        {
            return new IpcError(BuildErrorCodes.BuildUnityBuildProfileInvalid, message, null);
        }
    }
}
#endif
