#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Build;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class Unity6000BuildProfileInputResolverTests
    {
        private const string ProjectFingerprint = "unity-6000-build-profile-project";
        private const string RunId = "unity-6000-build-profile-run";
        private const string BuildProfileSettingsFolderPath = "Assets/Settings";
        private const string BuildProfileDefaultFolderPath = "Assets/Settings/Build Profiles";

        [Test]
        [Category("Size.Small")]
        public async Task ResolveAsync_WithRequestedBuildProfileAsset_AppliesAssetAndReturnsResolvedInput ()
        {
            using (new ActiveBuildProfileScope())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            using (var outputScope = TemporaryOutputScope.Create())
            {
                var inactiveScenePath = CreateSavedScene(editorScope, "Unity6000BuildProfileInactive");
                var requestedScenePath = CreateSavedScene(editorScope, "Unity6000BuildProfileRequested");
                var inactiveProfile = CreateBuildProfileAsset(
                    editorScope,
                    "Unity6000BuildProfileInactive",
                    new[] { inactiveScenePath },
                    scenesEnabled: true,
                    out _,
                    out _,
                    out _);
                var requestedProfile = CreateBuildProfileAsset(
                    editorScope,
                    "Unity6000BuildProfileRequested",
                    new[] { requestedScenePath },
                    scenesEnabled: true,
                    out var stableBuildTarget,
                    out var unityBuildTargetLiteral,
                    out var requestedProfilePath);
                BuildProfile.SetActiveBuildProfile(inactiveProfile);
                var resolver = CreateResolver();
                var request = CreateRequest(outputScope.OutputPath, requestedProfilePath);

                var result = await resolver.ResolveAsync(request, CancellationToken.None);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Error, Is.Null);
                Assert.That(result.PreconditionInput, Is.Not.Null);
                Assert.That(result.OutputLayout, Is.Not.Null);
                Assert.That(result.UnityBuildProfile, Is.Not.Null);

                var preconditionInput = result.PreconditionInput!;
                Assert.That(preconditionInput.InputKind, Is.EqualTo(ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile)));
                Assert.That(preconditionInput.BuildTarget, Is.EqualTo(stableBuildTarget));
                Assert.That(preconditionInput.UnityBuildTarget, Is.EqualTo(unityBuildTargetLiteral));
                Assert.That(preconditionInput.SceneSource, Is.EqualTo(ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile)));
                Assert.That(preconditionInput.ScenePaths, Is.EqualTo(new[] { requestedScenePath }));

                Assert.That(IpcBuildOutputLayoutResolver.TryResolve(
                    outputScope.OutputPath,
                    stableBuildTarget,
                    out var expectedOutputLayout), Is.True);
                var expectedLocationPathName = string.Equals(
                        stableBuildTarget,
                        ContractLiteralCodec.ToValue(BuildTargetStableName.Android),
                        StringComparison.Ordinal)
                    && EditorUserBuildSettings.buildAppBundle
                        ? ReplaceOutputFileName(expectedOutputLayout!.LocationPathName, "Player.aab")
                        : expectedOutputLayout!.LocationPathName;
                Assert.That(result.OutputLayout!.Shape, Is.EqualTo(expectedOutputLayout!.Shape));
                Assert.That(result.OutputLayout.LocationPathName, Is.EqualTo(expectedLocationPathName));

                var unityBuildProfile = result.UnityBuildProfile!;
                Assert.That(unityBuildProfile.Path, Is.EqualTo(requestedProfilePath));
                Assert.That(unityBuildProfile.Digest, Is.EqualTo(ComputeAssetDigest(requestedProfilePath)));
                Assert.That(unityBuildProfile.ApplyAudit, Is.Not.Null);

                var applyAudit = unityBuildProfile.ApplyAudit!;
                Assert.That(applyAudit.Applied, Is.True);
                Assert.That(applyAudit.LifecycleBefore.CompileGeneration, Is.EqualTo("build-profile-1-compile"));
                Assert.That(applyAudit.LifecycleAfter.CompileGeneration, Is.EqualTo("build-profile-2-compile"));
                Assert.That(applyAudit.LifecycleAfter.CompileGeneration, Is.Not.EqualTo(applyAudit.LifecycleBefore.CompileGeneration));
                Assert.That(applyAudit.LifecycleAfter.DomainReloadGeneration, Is.Not.EqualTo(applyAudit.LifecycleBefore.DomainReloadGeneration));
                Assert.That(applyAudit.LifecycleAfter.AssetRefreshGeneration, Is.Not.EqualTo(applyAudit.LifecycleBefore.AssetRefreshGeneration));
                Assert.That(applyAudit.GenerationsBefore.CompileGeneration, Is.EqualTo(applyAudit.LifecycleBefore.CompileGeneration));
                Assert.That(applyAudit.GenerationsBefore.DomainReloadGeneration, Is.EqualTo(applyAudit.LifecycleBefore.DomainReloadGeneration));
                Assert.That(applyAudit.GenerationsBefore.AssetRefreshGeneration, Is.EqualTo(applyAudit.LifecycleBefore.AssetRefreshGeneration));
                Assert.That(applyAudit.GenerationsAfter.CompileGeneration, Is.EqualTo(applyAudit.LifecycleAfter.CompileGeneration));
                Assert.That(applyAudit.GenerationsAfter.DomainReloadGeneration, Is.EqualTo(applyAudit.LifecycleAfter.DomainReloadGeneration));
                Assert.That(applyAudit.GenerationsAfter.AssetRefreshGeneration, Is.EqualTo(applyAudit.LifecycleAfter.AssetRefreshGeneration));
                Assert.That(applyAudit.DirtyStateAfter.Checked, Is.True);
                Assert.That(result.DirtyState, Is.SameAs(applyAudit.DirtyStateAfter));

                var activeProfile = BuildProfile.GetActiveBuildProfile();
                Assert.That(activeProfile != null, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(activeProfile), Is.EqualTo(AssetDatabase.GetAssetPath(requestedProfile)));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveAsync_WithPostApplyOutputLayoutFailure_ReturnsAppliedProfileEvidence ()
        {
            using (new ActiveBuildProfileScope())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            {
                var scenePath = CreateSavedScene(editorScope, "Unity6000BuildProfileOutputLayoutFailure");
                CreateBuildProfileAsset(
                    editorScope,
                    "Unity6000BuildProfileOutputLayoutFailure",
                    new[] { scenePath },
                    scenesEnabled: true,
                    out _,
                    out _,
                    out var profilePath);
                var resolver = CreateResolver();
                var request = CreateRequest(string.Empty, profilePath);

                var result = await resolver.ResolveAsync(request, CancellationToken.None);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildInputsInvalid));
                Assert.That(result.LifecycleBefore, Is.Not.Null);
                Assert.That(result.DirtyState, Is.Not.Null);
                Assert.That(result.UnityBuildProfile, Is.Not.Null);
                Assert.That(result.UnityBuildProfile!.Path, Is.EqualTo(profilePath));
                Assert.That(result.UnityBuildProfile.Digest, Is.EqualTo(ComputeAssetDigest(profilePath)));
                Assert.That(result.UnityBuildProfile.ApplyAudit, Is.Not.Null);
                Assert.That(result.UnityBuildProfile.ApplyAudit!.Applied, Is.True);
                Assert.That(result.LifecycleBefore, Is.SameAs(result.UnityBuildProfile.ApplyAudit.LifecycleAfter));
                Assert.That(result.DirtyState, Is.SameAs(result.UnityBuildProfile.ApplyAudit.DirtyStateAfter));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveAsync_WithDisabledProfileScene_ReturnsSceneDisabled ()
        {
            using (new ActiveBuildProfileScope())
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            using (var outputScope = TemporaryOutputScope.Create())
            {
                var scenePath = CreateSavedScene(editorScope, "Unity6000BuildProfileDisabled");
                CreateBuildProfileAsset(
                    editorScope,
                    "Unity6000BuildProfileDisabled",
                    new[] { scenePath },
                    scenesEnabled: false,
                    out _,
                    out _,
                    out var profilePath);
                var resolver = CreateResolver();
                var request = CreateRequest(outputScope.OutputPath, profilePath);

                var result = await resolver.ResolveAsync(request, CancellationToken.None);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildSceneDisabled));
                Assert.That(result.UnityBuildProfile, Is.Not.Null);
                Assert.That(result.UnityBuildProfile!.Path, Is.EqualTo(profilePath));
                Assert.That(result.UnityBuildProfile.Digest, Is.Null);
                Assert.That(result.UnityBuildProfile.ApplyAudit, Is.Null);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveAsync_WithMissingProfilePath_ReturnsBuildProfileInvalid ()
        {
            using (var outputScope = TemporaryOutputScope.Create())
            {
                var resolver = CreateResolver();
                var request = CreateRequest(outputScope.OutputPath, "Assets/MissingBuildProfile.asset");

                var result = await resolver.ResolveAsync(request, CancellationToken.None);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildUnityBuildProfileInvalid));
                Assert.That(result.UnityBuildProfile, Is.Not.Null);
                Assert.That(result.UnityBuildProfile!.Path, Is.EqualTo("Assets/MissingBuildProfile.asset"));
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveAsync_WithNonBuildProfileAsset_ReturnsBuildProfileInvalid ()
        {
            using (var editorScope = new EditorTestScope().SuppressExistingPersistentDirtyObjects())
            using (var outputScope = TemporaryOutputScope.Create())
            {
                editorScope.CreateScriptableAsset<NonBuildProfileAsset>(
                    "Unity6000BuildProfileWrongType",
                    out var profilePath);
                var resolver = CreateResolver();
                var request = CreateRequest(outputScope.OutputPath, profilePath);

                var result = await resolver.ResolveAsync(request, CancellationToken.None);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildUnityBuildProfileInvalid));
                Assert.That(result.UnityBuildProfile, Is.Not.Null);
                Assert.That(result.UnityBuildProfile!.Path, Is.EqualTo(profilePath));
            }
        }

        private static Unity6000BuildProfileInputResolver CreateResolver ()
        {
            return new Unity6000BuildProfileInputResolver(new UnityBuildPreconditionProbe(
                new CountingReadinessGate(),
                CreateProjectIdentity(),
                new StubServerVersionProvider("1.2.3"),
                new CountingBuildTargetSupportProbe()));
        }

        private static IpcProjectIdentity CreateProjectIdentity ()
        {
            return new IpcProjectIdentity(
                ProjectPath: ResolveProjectRootPath(),
                ProjectFingerprint: ProjectFingerprint,
                UnityVersion: Application.unityVersion);
        }

        private static IpcBuildRunRequest CreateRequest (
            string outputPath,
            string profilePath)
        {
            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                BuildTarget: null,
                UnityBuildTarget: null,
                SceneSource: null,
                ScenePaths: Array.Empty<string>(),
                Development: false,
                OutputPath: outputPath,
                OutputLayout: null,
                BuildReportPath: Path.Combine(outputPath, "build-report.json"),
                BuildLogPath: Path.Combine(outputPath, "build.log"),
                AllowedEditorModes: new[] { ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode) },
                ProjectMutationMode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Forbid),
                UnityBuildProfile: new IpcUnityBuildProfileInput(profilePath));
        }

        private static BuildProfile CreateBuildProfileAsset (
            EditorTestScope scope,
            string prefix,
            string[] scenePaths,
            bool scenesEnabled,
            out string stableBuildTarget,
            out string unityBuildTargetLiteral,
            out string profilePath)
        {
            var platforms = BuildProfile.GetInstalledPlatformModules();
            foreach (var platform in platforms)
            {
                var settingsFolderExisted = AssetDatabase.IsValidFolder(BuildProfileSettingsFolderPath);
                var defaultFolderExisted = AssetDatabase.IsValidFolder(BuildProfileDefaultFolderPath);
                var profile = BuildProfile.CreateBuildProfile(
                    platform.platformGuid,
                    prefix + "_" + Guid.NewGuid().ToString("N"),
                    null);
                if (profile == null)
                {
                    continue;
                }

                var createdPath = AssetDatabase.GetAssetPath(profile);
                profilePath = MoveProfileToTrackedAssetPath(
                    scope,
                    prefix,
                    createdPath,
                    settingsFolderExisted,
                    defaultFolderExisted);
                ConfigureProfileScenes(profile, scenePaths, scenesEnabled);
                BuildProfile.SetActiveBuildProfile(profile);
                if (!TryResolveStableBuildTarget(
                    EditorUserBuildSettings.activeBuildTarget,
                    out stableBuildTarget,
                    out unityBuildTargetLiteral))
                {
                    continue;
                }

                return profile;
            }

            Assert.Fail("No installed Unity Build Profile platform maps to a supported uCLI build target.");
            stableBuildTarget = string.Empty;
            unityBuildTargetLiteral = string.Empty;
            profilePath = string.Empty;
            throw new InvalidOperationException("No supported Unity Build Profile platform was available.");
        }

        private static string MoveProfileToTrackedAssetPath (
            EditorTestScope scope,
            string prefix,
            string createdPath,
            bool settingsFolderExisted,
            bool defaultFolderExisted)
        {
            var profilePath = scope.CreateAssetPath(prefix);
            var moveError = AssetDatabase.MoveAsset(createdPath, profilePath);
            Assert.That(moveError, Is.Empty);
            if (!defaultFolderExisted)
            {
                AssetDatabase.DeleteAsset(BuildProfileDefaultFolderPath);
            }

            if (!settingsFolderExisted)
            {
                AssetDatabase.DeleteAsset(BuildProfileSettingsFolderPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return profilePath;
        }

        private static void ConfigureProfileScenes (
            BuildProfile profile,
            string[] scenePaths,
            bool scenesEnabled)
        {
            var scenes = new EditorBuildSettingsScene[scenePaths.Length];
            for (var i = 0; i < scenePaths.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePaths[i], scenesEnabled);
            }

            profile.overrideGlobalScenes = true;
            profile.scenes = scenes;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool TryResolveStableBuildTarget (
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

        private static string CreateSavedScene (
            EditorTestScope scope,
            string prefix)
        {
            var scenePath = scope.CreateScenePath(prefix);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(prefix + "Root");
            SceneManager.MoveGameObjectToScene(root, scene);
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            return scenePath;
        }

        private static string ComputeAssetDigest (string assetPath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(
                ResolveProjectRootPath(),
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
            return Sha256LowerHex.Compute(File.ReadAllBytes(absolutePath));
        }

        private static string ReplaceOutputFileName (
            string locationPathName,
            string fileName)
        {
            var separatorIndex = locationPathName.LastIndexOf('/');
            return separatorIndex < 0
                ? fileName
                : string.Concat(locationPathName.Substring(0, separatorIndex + 1), fileName);
        }

        private static string ResolveProjectRootPath ()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static UnityEditorLifecycleSnapshot CreateLifecycleSnapshot (int captureIndex)
        {
            var generation = "build-profile-" + captureIndex;
            return new UnityEditorLifecycleSnapshot(
                EditorMode: DaemonEditorMode.Batchmode,
                LifecycleState: IpcEditorLifecycleStateCodec.Ready,
                BlockingReason: null,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: generation + "-compile",
                DomainReloadGeneration: generation + "-domain",
                CanAcceptExecutionRequests: true,
                ObservedAtUtc: DateTimeOffset.Parse("2026-06-20T00:00:00+00:00"),
                PlayMode: new IpcPlayModeSnapshot(
                    State: "stopped",
                    Transition: "none",
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false,
                    Generation: generation + "-play"),
                AssetRefreshGeneration: generation + "-asset");
        }

        private sealed class CountingReadinessGate : IUnityEditorReadinessGate
        {
            private int captureCount;

            public UnityEditorLifecycleSnapshot CaptureSnapshot ()
            {
                captureCount++;
                return CreateLifecycleSnapshot(captureCount);
            }

            public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
                bool failFast,
                CancellationToken cancellationToken = default,
                bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                captureCount++;
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(CreateLifecycleSnapshot(captureCount)));
            }
        }

        private sealed class CountingBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
        {
            public UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral)
            {
                return UnityBuildTargetSupportProbeResult.Resolved(
                    BuildTarget.StandaloneLinux64,
                    BuildTargetGroup.Standalone,
                    isSupported: true);
            }
        }

        private sealed class StubServerVersionProvider : IServerVersionProvider
        {
            private readonly string version;

            public StubServerVersionProvider (string version)
            {
                this.version = version;
            }

            public string GetVersion ()
            {
                return version;
            }
        }

        private sealed class ActiveBuildProfileScope : IDisposable
        {
            private readonly BuildProfile originalProfile;

            private bool disposed;

            public ActiveBuildProfileScope ()
            {
                originalProfile = BuildProfile.GetActiveBuildProfile();
            }

            public void Dispose ()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                if (originalProfile != null)
                {
                    BuildProfile.SetActiveBuildProfile(originalProfile);
                    return;
                }

                BuildProfile.SetActiveBuildProfile(null);
            }
        }

        private sealed class TemporaryOutputScope : IDisposable
        {
            private TemporaryOutputScope (string rootPath)
            {
                RootPath = rootPath;
                OutputPath = Path.Combine(rootPath, "build-output");
                Directory.CreateDirectory(OutputPath);
            }

            public string RootPath { get; }

            public string OutputPath { get; }

            public static TemporaryOutputScope Create ()
            {
                var rootPath = Path.Combine(Path.GetTempPath(), "ucli-unity-6000-build-profile-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rootPath);
                return new TemporaryOutputScope(rootPath);
            }

            public void Dispose ()
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
        }

        private sealed class NonBuildProfileAsset : ScriptableObject
        {
        }
    }
}
#endif
