using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Build;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityBuildPreconditionProbeTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenExplicitCleanSceneInputIsValid_ReturnsResolvedInput ()
        {
            using var scope = new EditorTestScope();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionClean", NewSceneMode.Single);
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(CreateExplicitInput(scenePath), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Checked, Is.True);
            Assert.That(result.DirtyState.Dirty, Is.False);
            Assert.That(result.DirtyState.Items, Is.Empty);
            Assert.That(result.ResolvedInput, Is.Not.Null);
            Assert.That(result.ResolvedInput!.Target, Is.EqualTo(BuildTarget.StandaloneLinux64));
            Assert.That(result.ResolvedInput.TargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
            Assert.That(result.ResolvedInput.ScenePaths, Is.EqualTo(new[] { scenePath }));
            Assert.That(result.ResolvedInput.Options, Is.EqualTo(BuildOptions.None));
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.UnityBuildTarget, Is.EqualTo("StandaloneLinux64"));
            Assert.That(result.InputProbe.UnityBuildTargetGroup, Is.EqualTo("Standalone"));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenInputScenesAreDirty_ReturnsOrderedDirtyState ()
        {
            using var scope = new EditorTestScope();
            var (zScenePath, zScene) = CreateSavedScene(scope, "ZBuildPreconditionDirty", NewSceneMode.Single);
            var (aScenePath, aScene) = CreateSavedScene(scope, "ABuildPreconditionDirty", NewSceneMode.Additive);
            MarkSceneDirty(zScene, "DirtyZ");
            MarkSceneDirty(aScene, "DirtyA");
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(zScenePath, aScenePath),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildDirtyStatePresent));
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Checked, Is.True);
            Assert.That(result.DirtyState.Dirty, Is.True);
            Assert.That(result.DirtyState.Items, Has.Count.EqualTo(2));
            Assert.That(result.DirtyState.Items[0].Kind, Is.EqualTo(IpcBuildDirtyStateItemKindNames.Scene));
            Assert.That(result.DirtyState.Items[0].Path, Is.EqualTo(aScenePath));
            Assert.That(result.DirtyState.Items[1].Path, Is.EqualTo(zScenePath));
            Assert.That(result.ResolvedInput, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenNonInputSceneAndPrefabStageAreDirty_IgnoresThem ()
        {
            using var scope = new EditorTestScope().EnablePrefabStageCleanup();
            var (targetScenePath, _) = CreateSavedScene(scope, "BuildPreconditionTarget", NewSceneMode.Single);
            var (_, unrelatedScene) = CreateSavedScene(scope, "BuildPreconditionUnrelated", NewSceneMode.Additive);
            MarkSceneDirty(unrelatedScene, "UnrelatedDirty");
            var prefabPath = scope.CreatePrefabAsset(nameof(UnityBuildPreconditionProbeTests), "PrefabRoot");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var dirtyPrefabChild = new GameObject("DirtyPrefabChild");
            dirtyPrefabChild.transform.SetParent(prefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(CreateExplicitInput(targetScenePath), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Checked, Is.True);
            Assert.That(result.DirtyState.Dirty, Is.False);
            Assert.That(result.DirtyState.Items, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenEditorBuildSettingsSourceIsUsed_UsesEnabledScenesOnly ()
        {
            using var scope = new EditorTestScope();
            using var editorBuildSettingsScope = new EditorBuildSettingsScope();
            var (enabledScenePath, _) = CreateSavedScene(scope, "BuildPreconditionEnabled", NewSceneMode.Single);
            var (disabledScenePath, disabledScene) = CreateSavedScene(scope, "BuildPreconditionDisabled", NewSceneMode.Additive);
            MarkSceneDirty(disabledScene, "DisabledDirty");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(disabledScenePath, enabled: false),
                new EditorBuildSettingsScene(enabledScenePath, enabled: true),
            };
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                new UnityBuildPreconditionInput(
                    TargetStableName: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    SceneSource: SceneSourceLiteral(BuildProfileSceneSource.EditorBuildSettings),
                    ScenePaths: Array.Empty<string>(),
                    Development: true),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.ResolvedInput, Is.Not.Null);
            Assert.That(result.ResolvedInput!.ScenePaths, Is.EqualTo(new[] { enabledScenePath }));
            Assert.That(result.ResolvedInput.Options, Is.EqualTo(BuildOptions.Development));
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Dirty, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenSceneSourceIsInvalid_ReturnsBuildInputsInvalid ()
        {
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                new UnityBuildPreconditionInput(
                    TargetStableName: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    SceneSource: "unsupported",
                    ScenePaths: Array.Empty<string>(),
                    Development: false),
                CancellationToken.None);

            AssertBuildInputsInvalidResult(result);
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.SceneSource, Is.EqualTo("unsupported"));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenExplicitSceneInputIsEmpty_ReturnsBuildInputsInvalid ()
        {
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                new UnityBuildPreconditionInput(
                    TargetStableName: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    SceneSource: SceneSourceLiteral(BuildProfileSceneSource.Explicit),
                    ScenePaths: Array.Empty<string>(),
                    Development: false),
                CancellationToken.None);

            AssertBuildInputsInvalidResult(result);
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.Scenes, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenEditorBuildSettingsHasNoEnabledScenes_ReturnsBuildInputsInvalid ()
        {
            using var editorBuildSettingsScope = new EditorBuildSettingsScope();
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                new UnityBuildPreconditionInput(
                    TargetStableName: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    SceneSource: SceneSourceLiteral(BuildProfileSceneSource.EditorBuildSettings),
                    ScenePaths: Array.Empty<string>(),
                    Development: false),
                CancellationToken.None);

            AssertBuildInputsInvalidResult(result);
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.SceneSource, Is.EqualTo(SceneSourceLiteral(BuildProfileSceneSource.EditorBuildSettings)));
        }

        [Test]
        [Category("Size.Small")]
        [TestCase(" Assets/Scenes/Main.unity")]
        [TestCase("Assets/Scenes/Main.unity ")]
        [TestCase("Assets\\Scenes\\Main.unity")]
        [TestCase("Assets/../Scenes/Main.unity")]
        [TestCase("/Assets/Scenes/Main.unity")]
        [TestCase("C:/Project/Assets/Scenes/Main.unity")]
        [TestCase("Packages/Scenes/Main.unity")]
        [TestCase("Assets/Scenes/Missing.unity")]
        public async Task ProbeBeforeBuildAsync_WhenExplicitScenePathIsInvalid_ReturnsBuildInputsInvalid (string scenePath)
        {
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(CreateExplicitInput(scenePath), CancellationToken.None);

            AssertBuildInputsInvalidResult(result);
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.Scenes, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenExplicitScenePathCaseDiffersFromAssetPath_ReturnsBuildInputsInvalid ()
        {
            using var scope = new EditorTestScope();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionCanonicalCase", NewSceneMode.Single);
            var caseMismatchedPath = "Assets/" + scenePath.Substring("Assets/".Length).ToLowerInvariant();
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(CreateExplicitInput(caseMismatchedPath), CancellationToken.None);

            Assert.That(caseMismatchedPath, Is.Not.EqualTo(scenePath));
            AssertBuildInputsInvalidResult(result);
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.Scenes, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenTargetModuleIsMissing_ReturnsBuildTargetModuleMissing ()
        {
            var probe = CreateProbe(
                targetSupportProbe: new StubBuildTargetSupportProbe(
                    UnityBuildTargetSupportProbeResult.Resolved(
                        BuildTarget.Android,
                        BuildTargetGroup.Android,
                        isSupported: false)));

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput("Assets/Scenes/NotReached.unity"),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildTargetModuleMissing));
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.UnityBuildTarget, Is.EqualTo("Android"));
            Assert.That(result.ResolvedInput, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        [TestCase("999999")]
        [TestCase(" StandaloneLinux64")]
        [TestCase("StandaloneLinux64 ")]
        public void UnityBuildTargetSupportProbe_WhenLiteralIsNotCanonical_ReturnsInvalid (string unityBuildTargetLiteral)
        {
            var result = new UnityBuildTargetSupportProbe().Probe(unityBuildTargetLiteral);

            Assert.That(result.IsValidTarget, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void UnityBuildTargetSupportProbe_WhenLiteralIsCanonical_ReturnsResolvedTarget ()
        {
            var result = new UnityBuildTargetSupportProbe().Probe("StandaloneLinux64");

            Assert.That(result.IsValidTarget, Is.True);
            Assert.That(result.Target, Is.EqualTo(BuildTarget.StandaloneLinux64));
            Assert.That(result.TargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenTargetLiteralIsInvalid_ReturnsBuildInputsInvalid ()
        {
            var probe = CreateProbe(
                targetSupportProbe: new StubBuildTargetSupportProbe(UnityBuildTargetSupportProbeResult.Invalid()));

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput("Assets/Scenes/NotReached.unity"),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildInputsInvalid));
            Assert.That(result.InputProbe, Is.Null);
            Assert.That(result.ResolvedInput, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void ProbeBeforeBuildAsync_WhenCanceledAfterReadiness_PropagatesCancellation ()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var probe = CreateProbe(
                targetSupportProbe: new CancelingBuildTargetSupportProbe(
                    cancellationTokenSource,
                    UnityBuildTargetSupportProbeResult.Resolved(
                        BuildTarget.StandaloneLinux64,
                        BuildTargetGroup.Standalone,
                        isSupported: true)));

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await probe.ProbeBeforeBuildAsync(
                    CreateExplicitInput("Assets/Scenes/NotReached.unity"),
                    cancellationTokenSource.Token));
        }

        [Test]
        [Category("Size.Small")]
        public async Task CaptureAfterBuild_WhenSnapshotChanges_ReturnsIndependentAfterSnapshot ()
        {
            using var scope = new EditorTestScope();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionLifecycle", NewSceneMode.Single);
            var readinessGate = new MutableReadinessGate(CreateSnapshot(
                compileGeneration: "compile-before",
                domainReloadGeneration: "domain-before"));
            var probe = CreateProbe(readinessGate);

            var beforeResult = await probe.ProbeBeforeBuildAsync(CreateExplicitInput(scenePath), CancellationToken.None);
            readinessGate.Snapshot = CreateSnapshot(
                compileGeneration: "compile-after",
                domainReloadGeneration: "domain-after");
            var after = probe.CaptureAfterBuild();

            Assert.That(beforeResult.IsSuccess, Is.True, beforeResult.Error?.Message);
            Assert.That(beforeResult.LifecycleBefore.CompileGeneration, Is.EqualTo("compile-before"));
            Assert.That(beforeResult.LifecycleBefore.DomainReloadGeneration, Is.EqualTo("domain-before"));
            Assert.That(after.CompileGeneration, Is.EqualTo("compile-after"));
            Assert.That(after.DomainReloadGeneration, Is.EqualTo("domain-after"));
            Assert.That(readinessGate.CaptureSnapshotCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenReadinessIsBlocked_ReturnsLifecycleFailureWithoutTargetProbe ()
        {
            var readinessError = new IpcError(UcliCoreErrorCodes.InternalError, "readiness blocked", null);
            var readinessGate = new MutableReadinessGate(
                CreateSnapshot(
                    compileGeneration: "compile-blocked",
                    domainReloadGeneration: "domain-blocked"),
                readinessError);
            var targetSupportProbe = new CountingBuildTargetSupportProbe(
                UnityBuildTargetSupportProbeResult.Resolved(
                    BuildTarget.StandaloneLinux64,
                    BuildTargetGroup.Standalone,
                    isSupported: true));
            var probe = CreateProbe(readinessGate, targetSupportProbe);

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput("Assets/Scenes/NotReached.unity"),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.SameAs(readinessError));
            Assert.That(result.LifecycleBefore.CompileGeneration, Is.EqualTo("compile-blocked"));
            Assert.That(result.LifecycleBefore.DomainReloadGeneration, Is.EqualTo("domain-blocked"));
            Assert.That(result.DirtyState, Is.Null);
            Assert.That(result.InputProbe, Is.Null);
            Assert.That(result.ResolvedInput, Is.Null);
            Assert.That(targetSupportProbe.CallCount, Is.EqualTo(0));
        }

        private static UnityBuildPreconditionProbe CreateProbe (
            MutableReadinessGate? readinessGate = null,
            IUnityBuildTargetSupportProbe? targetSupportProbe = null)
        {
            return new UnityBuildPreconditionProbe(
                readinessGate ?? new MutableReadinessGate(CreateSnapshot()),
                new IpcProjectIdentity(
                    ProjectPath: "/project",
                    ProjectFingerprint: "project-fingerprint",
                    UnityVersion: "6000.0.0f1"),
                new StubServerVersionProvider("1.2.3"),
                targetSupportProbe ?? new StubBuildTargetSupportProbe(
                    UnityBuildTargetSupportProbeResult.Resolved(
                        BuildTarget.StandaloneLinux64,
                        BuildTargetGroup.Standalone,
                        isSupported: true)));
        }

        private static UnityBuildPreconditionInput CreateExplicitInput (params string[] scenePaths)
        {
            return new UnityBuildPreconditionInput(
                TargetStableName: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                SceneSource: SceneSourceLiteral(BuildProfileSceneSource.Explicit),
                ScenePaths: scenePaths,
                Development: false);
        }

        private static void AssertBuildInputsInvalidResult (UnityBuildPreconditionProbeResult result)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildInputsInvalid));
            Assert.That(result.DirtyState, Is.Null);
            Assert.That(result.ResolvedInput, Is.Null);
        }

        private static string SceneSourceLiteral (BuildProfileSceneSource source)
        {
            return ContractLiteralCodec.ToValue(source);
        }

        private static (string Path, Scene Scene) CreateSavedScene (
            EditorTestScope scope,
            string prefix,
            NewSceneMode mode)
        {
            var scenePath = scope.CreateScenePath(prefix);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            var root = new GameObject(prefix + "Root");
            SceneManager.MoveGameObjectToScene(root, scene);
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            return (scenePath, scene);
        }

        private static void MarkSceneDirty (Scene scene, string objectName)
        {
            var gameObject = new GameObject(objectName);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(scene.isDirty, Is.True);
        }

        private static UnityEditorLifecycleSnapshot CreateSnapshot (
            string compileGeneration = "compile-1",
            string domainReloadGeneration = "domain-1")
        {
            return new UnityEditorLifecycleSnapshot(
                EditorMode: DaemonEditorMode.Batchmode,
                LifecycleState: IpcEditorLifecycleStateCodec.Ready,
                BlockingReason: null,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: compileGeneration,
                DomainReloadGeneration: domainReloadGeneration,
                CanAcceptExecutionRequests: true,
                ObservedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                PlayMode: new IpcPlayModeSnapshot(
                    State: "stopped",
                    Transition: "none",
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false,
                    Generation: "play-1"));
        }

        private sealed class MutableReadinessGate : IUnityEditorReadinessGate
        {
            private readonly IpcError? error;

            public MutableReadinessGate (
                UnityEditorLifecycleSnapshot snapshot,
                IpcError? error = null)
            {
                Snapshot = snapshot;
                this.error = error;
            }

            public UnityEditorLifecycleSnapshot Snapshot { get; set; }

            public int CaptureSnapshotCallCount { get; private set; }

            public UnityEditorLifecycleSnapshot CaptureSnapshot ()
            {
                CaptureSnapshotCallCount++;
                return Snapshot;
            }

            public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
                bool failFast,
                CancellationToken cancellationToken = default,
                bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(error == null
                    ? UnityEditorExecutionReadinessResult.Ready(Snapshot)
                    : UnityEditorExecutionReadinessResult.Blocked(Snapshot, error));
            }
        }

        private sealed class StubBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
        {
            private readonly UnityBuildTargetSupportProbeResult result;

            public StubBuildTargetSupportProbe (UnityBuildTargetSupportProbeResult result)
            {
                this.result = result;
            }

            public UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral)
            {
                return result;
            }
        }

        private sealed class CountingBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
        {
            private readonly UnityBuildTargetSupportProbeResult result;

            public CountingBuildTargetSupportProbe (UnityBuildTargetSupportProbeResult result)
            {
                this.result = result;
            }

            public int CallCount { get; private set; }

            public UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral)
            {
                CallCount++;
                return result;
            }
        }

        private sealed class CancelingBuildTargetSupportProbe : IUnityBuildTargetSupportProbe
        {
            private readonly CancellationTokenSource cancellationTokenSource;
            private readonly UnityBuildTargetSupportProbeResult result;

            public CancelingBuildTargetSupportProbe (
                CancellationTokenSource cancellationTokenSource,
                UnityBuildTargetSupportProbeResult result)
            {
                this.cancellationTokenSource = cancellationTokenSource;
                this.result = result;
            }

            public UnityBuildTargetSupportProbeResult Probe (string unityBuildTargetLiteral)
            {
                cancellationTokenSource.Cancel();
                return result;
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

        private sealed class EditorBuildSettingsScope : IDisposable
        {
            private readonly EditorBuildSettingsScene[] originalScenes;

            public EditorBuildSettingsScope ()
            {
                originalScenes = EditorBuildSettings.scenes;
            }

            public void Dispose ()
            {
                EditorBuildSettings.scenes = originalScenes;
            }
        }
    }
}
