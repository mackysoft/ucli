using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
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
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionClean", NewSceneMode.Single);
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath(scenePath)),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Error, Is.Null);
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Checked, Is.True);
            Assert.That(result.DirtyState.Dirty, Is.False);
            Assert.That(result.DirtyState.Coverage, Is.EqualTo(IpcBuildDirtyStateCoverage.Full));
            Assert.That(result.DirtyState.Items, Is.Empty);
            Assert.That(result.ResolvedInput, Is.Not.Null);
            Assert.That(result.ResolvedInput!.UnityBuildTarget, Is.EqualTo(BuildTarget.StandaloneLinux64));
            Assert.That(result.ResolvedInput.UnityBuildTargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
            Assert.That(result.ResolvedInput.ScenePaths, Is.EqualTo(new[] { new SceneAssetPath(scenePath) }));
            Assert.That(result.ResolvedInput.Options, Is.EqualTo(BuildOptions.None));
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.UnityBuildTarget, Is.EqualTo("StandaloneLinux64"));
            Assert.That(result.InputProbe.UnityBuildTargetGroup, Is.EqualTo("Standalone"));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenInputScenesAreDirty_ReturnsOrderedDirtyState ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var (zScenePath, zScene) = CreateSavedScene(scope, "ZBuildPreconditionDirty", NewSceneMode.Single);
            var (aScenePath, aScene) = CreateSavedScene(scope, "ABuildPreconditionDirty", NewSceneMode.Additive);
            MarkSceneDirty(zScene, "DirtyZ");
            MarkSceneDirty(aScene, "DirtyA");
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath(zScenePath), new SceneAssetPath(aScenePath)),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildDirtyStatePresent));
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Checked, Is.True);
            Assert.That(result.DirtyState.Dirty, Is.True);
            Assert.That(result.DirtyState.Coverage, Is.EqualTo(IpcBuildDirtyStateCoverage.Full));
            Assert.That(result.DirtyState.Items, Has.Count.EqualTo(2));
            Assert.That(result.DirtyState.Items[0].Kind, Is.EqualTo(IpcBuildDirtyStateItemKind.Scene));
            Assert.That(result.DirtyState.Items[0].Path, Is.EqualTo(aScenePath));
            Assert.That(result.DirtyState.Items[1].Path, Is.EqualTo(zScenePath));
            Assert.That(result.ResolvedInput, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenLoadedNonInputSceneIsDirty_ReturnsDirtyState ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var (targetScenePath, _) = CreateSavedScene(scope, "BuildPreconditionTarget", NewSceneMode.Single);
            var (unrelatedScenePath, unrelatedScene) = CreateSavedScene(scope, "BuildPreconditionUnrelated", NewSceneMode.Additive);
            MarkSceneDirty(unrelatedScene, "UnrelatedDirty");
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath(targetScenePath)),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildDirtyStatePresent));
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Checked, Is.True);
            Assert.That(result.DirtyState.Dirty, Is.True);
            Assert.That(result.DirtyState.Coverage, Is.EqualTo(IpcBuildDirtyStateCoverage.Full));
            Assert.That(result.DirtyState.Items, Has.Count.EqualTo(1));
            Assert.That(result.DirtyState.Items[0].Kind, Is.EqualTo(IpcBuildDirtyStateItemKind.Scene));
            Assert.That(result.DirtyState.Items[0].Path, Is.EqualTo(unrelatedScenePath));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenPersistentAssetsAreDirty_ReturnsClassifiedOrderedItems ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionPersistentAssetTarget", NewSceneMode.Single);
            var prefabPath = scope.CreatePrefabAsset(nameof(UnityBuildPreconditionProbeTests), "PrefabRoot");
            var asset = scope.CreateScriptableAsset<BuildPreconditionDirtyAsset>(nameof(UnityBuildPreconditionProbeTests), out var assetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            asset.dirtyMarker = "Dirty";
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(prefab);
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath(scenePath)),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildDirtyStatePresent));
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Coverage, Is.EqualTo(IpcBuildDirtyStateCoverage.Full));
            Assert.That(result.DirtyState.Items, Has.Count.EqualTo(2));
            AssertOrderedByPath(result.DirtyState.Items);
            var assetItem = FindDirtyItem(result.DirtyState.Items, assetPath);
            var prefabItem = FindDirtyItem(result.DirtyState.Items, prefabPath);
            Assert.That(assetItem.Kind, Is.EqualTo(IpcBuildDirtyStateItemKind.Asset));
            Assert.That(prefabItem.Kind, Is.EqualTo(IpcBuildDirtyStateItemKind.Prefab));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenProjectSettingsAssetIsDirty_ReturnsProjectSettingsItem ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionProjectSettingsTarget", NewSceneMode.Single);
            const string projectSettingsPath = "ProjectSettings/TagManager.asset";
            var projectSettingsAsset = AssetDatabase.LoadMainAssetAtPath(projectSettingsPath);
            Assert.That(projectSettingsAsset == null, Is.False);
            EditorUtility.SetDirty(projectSettingsAsset);
            var probe = CreateProbe();

            try
            {
                var result = await probe.ProbeBeforeBuildAsync(
                    CreateExplicitInput(new SceneAssetPath(scenePath)),
                    CancellationToken.None);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Error, Is.Not.Null);
                Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildDirtyStatePresent));
                Assert.That(result.DirtyState, Is.Not.Null);
                Assert.That(result.DirtyState!.Coverage, Is.EqualTo(IpcBuildDirtyStateCoverage.Full));
                Assert.That(result.DirtyState.Items, Has.Count.EqualTo(1));
                Assert.That(result.DirtyState.Items[0].Path, Is.EqualTo(projectSettingsPath));
                Assert.That(result.DirtyState.Items[0].Kind, Is.EqualTo(IpcBuildDirtyStateItemKind.ProjectSettings));
            }
            finally
            {
                EditorUtility.ClearDirty(projectSettingsAsset);
            }
        }

        [Test]
        [Category("Size.Small")]
        [TestCase("Assets/Any.asset", true)]
        [TestCase("ProjectSettings/TagManager.asset", true)]
        [TestCase("Packages/com.mackysoft.ucli.missing-dirty-state-test-6f3d4c9e/Runtime/Generated.asset", false)]
        [TestCase("Library/PackageCache/com.unity.render-pipelines.universal/Shaders/Unlit.shader", false)]
        public void IsPersistentDirtyObjectAuditedPath_WhenPathIsClassified_ReturnsExpected (
            string path,
            bool expected)
        {
            Assert.That(UnityBuildPreconditionProbe.IsPersistentDirtyObjectAuditedPath(path), Is.EqualTo(expected));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenEditorBuildSettingsSourceIsUsed_UsesEnabledScenesOnly ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            using var editorBuildSettingsScope = new EditorBuildSettingsScope();
            var (enabledScenePath, _) = CreateSavedScene(scope, "BuildPreconditionEnabled", NewSceneMode.Single);
            var (disabledScenePath, _) = CreateSavedScene(scope, "BuildPreconditionDisabled", NewSceneMode.Additive);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(disabledScenePath, enabled: false),
                new EditorBuildSettingsScene(enabledScenePath, enabled: true),
            };
            scope.SuppressExistingPersistentDirtyObjects();
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                new UnityBuildPreconditionInput(
                    InputKind: BuildProfileInputsKind.Explicit,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    SceneSource: BuildProfileSceneSource.EditorBuildSettings,
                    ScenePaths: Array.Empty<SceneAssetPath>(),
                    AllowedEditorModes: AllowedBatchmodeEditorModes(),
                    Development: true),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.ResolvedInput, Is.Not.Null);
            Assert.That(result.ResolvedInput!.ScenePaths, Is.EqualTo(new[] { new SceneAssetPath(enabledScenePath) }));
            Assert.That(result.ResolvedInput.Options, Is.EqualTo(BuildOptions.Development));
            Assert.That(result.DirtyState, Is.Not.Null);
            Assert.That(result.DirtyState!.Dirty, Is.False);
            Assert.That(result.DirtyState.Coverage, Is.EqualTo(IpcBuildDirtyStateCoverage.Full));
        }

        [Test]
        [Category("Size.Small")]
        public void UnityBuildPreconditionInput_WhenSceneSourceIsUndefined_Throws ()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityBuildPreconditionInput(
                    InputKind: BuildProfileInputsKind.Explicit,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    SceneSource: (BuildProfileSceneSource)0,
                    ScenePaths: Array.Empty<SceneAssetPath>(),
                    AllowedEditorModes: AllowedBatchmodeEditorModes(),
                    Development: false));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenExplicitSceneInputIsEmpty_ReturnsBuildInputsInvalid ()
        {
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                new UnityBuildPreconditionInput(
                    InputKind: BuildProfileInputsKind.Explicit,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    SceneSource: BuildProfileSceneSource.Explicit,
                    ScenePaths: Array.Empty<SceneAssetPath>(),
                    AllowedEditorModes: AllowedBatchmodeEditorModes(),
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
                    InputKind: BuildProfileInputsKind.Explicit,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    SceneSource: BuildProfileSceneSource.EditorBuildSettings,
                    ScenePaths: Array.Empty<SceneAssetPath>(),
                    AllowedEditorModes: AllowedBatchmodeEditorModes(),
                    Development: false),
                CancellationToken.None);

            AssertBuildInputsInvalidResult(result);
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.SceneSource, Is.EqualTo(BuildProfileSceneSource.EditorBuildSettings));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenExplicitScenePathDoesNotExist_ReturnsBuildSceneNotFound ()
        {
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath("Assets/Scenes/Missing.unity")),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildSceneNotFound));
            Assert.That(result.DirtyState, Is.Null);
            Assert.That(result.ResolvedInput, Is.Null);
            Assert.That(result.InputProbe, Is.Not.Null);
            Assert.That(result.InputProbe!.Scenes, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenExplicitScenePathCaseDiffersFromAssetPath_ReturnsBuildInputsInvalid ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionCanonicalCase", NewSceneMode.Single);
            var caseMismatchedPath = "Assets/" + scenePath.Substring("Assets/".Length).ToLowerInvariant();
            var probe = CreateProbe();

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath(caseMismatchedPath)),
                CancellationToken.None);

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
                CreateExplicitInput(new SceneAssetPath("Assets/Scenes/NotReached.unity")),
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
        public void UnityBuildTargetSupportProbe_WhenStableTargetIsUndefined_ReturnsInvalid ()
        {
            var result = new UnityBuildTargetSupportProbe().Probe(default);

            Assert.That(result.IsValidTarget, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void UnityBuildTargetSupportProbe_WhenStableTargetIsDefined_ReturnsResolvedTarget ()
        {
            var result = new UnityBuildTargetSupportProbe().Probe(BuildTargetStableName.StandaloneLinux64);

            Assert.That(result.IsValidTarget, Is.True);
            Assert.That(result.UnityBuildTarget, Is.EqualTo(BuildTarget.StandaloneLinux64));
            Assert.That(result.UnityBuildTargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenTargetLiteralIsInvalid_ReturnsBuildInputsInvalid ()
        {
            var probe = CreateProbe(
                targetSupportProbe: new StubBuildTargetSupportProbe(UnityBuildTargetSupportProbeResult.Invalid()));

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath("Assets/Scenes/NotReached.unity")),
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
                    CreateExplicitInput(new SceneAssetPath("Assets/Scenes/NotReached.unity")),
                    cancellationTokenSource.Token));
        }

        [Test]
        [Category("Size.Small")]
        public async Task CaptureAfterBuild_WhenSnapshotChanges_ReturnsIndependentAfterSnapshot ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var (scenePath, _) = CreateSavedScene(scope, "BuildPreconditionLifecycle", NewSceneMode.Single);
            var readinessGate = new MutableReadinessGate(CreateSnapshot(
                compileGeneration: 11,
                domainReloadGeneration: 12));
            var probe = CreateProbe(readinessGate);

            var beforeResult = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath(scenePath)),
                CancellationToken.None);
            readinessGate.Snapshot = CreateSnapshot(
                compileGeneration: 21,
                domainReloadGeneration: 22);
            var after = probe.CaptureAfterBuild();

            Assert.That(beforeResult.IsSuccess, Is.True, beforeResult.Error?.Message);
            Assert.That(beforeResult.LifecycleBefore.State.Generations.CompileGeneration, Is.EqualTo(11));
            Assert.That(beforeResult.LifecycleBefore.State.Generations.DomainReloadGeneration, Is.EqualTo(12));
            Assert.That(after.State.Generations.CompileGeneration, Is.EqualTo(21));
            Assert.That(after.State.Generations.DomainReloadGeneration, Is.EqualTo(22));
            Assert.That(readinessGate.CaptureObservationCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenReadinessIsBlocked_ReturnsLifecycleFailureWithoutTargetProbe ()
        {
            var readinessError = new IpcError(UcliCoreErrorCodes.InternalError, "readiness blocked", null);
            var readinessGate = new MutableReadinessGate(
                CreateSnapshot(
                    compileGeneration: 31,
                    domainReloadGeneration: 32),
                readinessError);
            var targetSupportProbe = new CountingBuildTargetSupportProbe(
                UnityBuildTargetSupportProbeResult.Resolved(
                    BuildTarget.StandaloneLinux64,
                    BuildTargetGroup.Standalone,
                    isSupported: true));
            var probe = CreateProbe(readinessGate, targetSupportProbe);

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath("Assets/Scenes/NotReached.unity")),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.SameAs(readinessError));
            Assert.That(result.LifecycleBefore.State.Generations.CompileGeneration, Is.EqualTo(31));
            Assert.That(result.LifecycleBefore.State.Generations.DomainReloadGeneration, Is.EqualTo(32));
            Assert.That(result.DirtyState, Is.Null);
            Assert.That(result.InputProbe, Is.Null);
            Assert.That(result.ResolvedInput, Is.Null);
            Assert.That(targetSupportProbe.CallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ProbeBeforeBuildAsync_WhenEditorModeIsDisallowed_ReturnsRuntimePolicyViolationBeforeTargetProbe ()
        {
            var readinessGate = new MutableReadinessGate(CreateSnapshot(editorMode: DaemonEditorMode.Gui));
            var targetSupportProbe = new CountingBuildTargetSupportProbe(
                UnityBuildTargetSupportProbeResult.Resolved(
                    BuildTarget.StandaloneLinux64,
                    BuildTargetGroup.Standalone,
                    isSupported: true));
            var probe = CreateProbe(readinessGate, targetSupportProbe);

            var result = await probe.ProbeBeforeBuildAsync(
                CreateExplicitInput(new SceneAssetPath("Assets/Scenes/NotReached.unity")),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildRuntimePolicyViolation));
            Assert.That(result.DirtyState, Is.Null);
            Assert.That(result.InputProbe, Is.Null);
            Assert.That(result.ResolvedInput, Is.Null);
            Assert.That(targetSupportProbe.CallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public void ProjectMutationAuditProbe_WhenProjectFilesChange_ReturnsAddedModifiedDeletedItems ()
        {
            using var scope = new EditorTestScope().SuppressExistingPersistentDirtyObjects();
            var projectRootPath = Path.GetDirectoryName(Application.dataPath);
            Assert.That(string.IsNullOrWhiteSpace(projectRootPath), Is.False);
            var addedPath = scope.CreateAssetPath(nameof(UnityBuildPreconditionProbeTests), ".txt");
            var modifiedPath = scope.CreateAssetPath(nameof(UnityBuildPreconditionProbeTests), ".txt");
            var deletedPath = scope.CreateAssetPath(nameof(UnityBuildPreconditionProbeTests), ".txt");
            WriteProjectFile(projectRootPath!, modifiedPath, "before");
            WriteProjectFile(projectRootPath!, deletedPath, "before");
            AssetDatabase.ImportAsset(modifiedPath);
            AssetDatabase.ImportAsset(deletedPath);
            var probe = new UnityProjectMutationAuditProbe();
            var baseline = probe.CaptureBaseline(projectRootPath!);

            WriteProjectFile(projectRootPath!, addedPath, "added");
            WriteProjectFile(projectRootPath!, modifiedPath, "after");
            AssetDatabase.ImportAsset(addedPath);
            AssetDatabase.ImportAsset(modifiedPath);
            Assert.That(AssetDatabase.DeleteAsset(deletedPath), Is.True);

            var audit = probe.Complete(
                projectRootPath!,
                BuildProfileProjectMutationMode.Audit,
                baseline);

            Assert.That(audit.Mode, Is.EqualTo(BuildProfileProjectMutationMode.Audit));
            Assert.That(audit.Coverage, Is.EqualTo(IpcBuildProjectMutationAuditCoverage.Full));
            Assert.That(audit.Mutated, Is.True);
            Assert.That(audit.BeforeDigest, Is.Not.EqualTo(audit.AfterDigest));
            AssertOrderedByPath(audit.Items);
            var addedItem = FindMutationItem(audit.Items, addedPath);
            var modifiedItem = FindMutationItem(audit.Items, modifiedPath);
            var deletedItem = FindMutationItem(audit.Items, deletedPath);
            Assert.That(addedItem.ChangeKind, Is.EqualTo(IpcBuildProjectMutationChangeKind.Added));
            Assert.That(addedItem.BeforeSha256, Is.Null);
            Assert.That(addedItem.AfterSha256, Is.Not.Null);
            Assert.That(modifiedItem.ChangeKind, Is.EqualTo(IpcBuildProjectMutationChangeKind.Modified));
            Assert.That(modifiedItem.BeforeSha256, Is.Not.Null);
            Assert.That(modifiedItem.AfterSha256, Is.Not.Null);
            Assert.That(modifiedItem.BeforeSha256, Is.Not.EqualTo(modifiedItem.AfterSha256));
            Assert.That(deletedItem.ChangeKind, Is.EqualTo(IpcBuildProjectMutationChangeKind.Deleted));
            Assert.That(deletedItem.BeforeSha256, Is.Not.Null);
            Assert.That(deletedItem.AfterSha256, Is.Null);
        }

        private static UnityBuildPreconditionProbe CreateProbe (
            MutableReadinessGate? readinessGate = null,
            IUnityBuildTargetSupportProbe? targetSupportProbe = null)
        {
            return new UnityBuildPreconditionProbe(
                readinessGate ?? new MutableReadinessGate(CreateSnapshot()),
                new IpcProjectIdentity(
                    projectPath: "/project",
                    projectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                    unityVersion: "6000.0.0f1"),
                new StubServerVersionProvider("1.2.3"),
                targetSupportProbe ?? new StubBuildTargetSupportProbe(
                    UnityBuildTargetSupportProbeResult.Resolved(
                        BuildTarget.StandaloneLinux64,
                        BuildTargetGroup.Standalone,
                        isSupported: true)));
        }

        private static UnityBuildPreconditionInput CreateExplicitInput (params SceneAssetPath[] scenePaths)
        {
            return new UnityBuildPreconditionInput(
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: scenePaths,
                AllowedEditorModes: AllowedBatchmodeEditorModes(),
                Development: false);
        }

        private static DaemonEditorMode[] AllowedBatchmodeEditorModes ()
        {
            return new[] { DaemonEditorMode.Batchmode };
        }

        private static void AssertBuildInputsInvalidResult (UnityBuildPreconditionProbeResult result)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildInputsInvalid));
            Assert.That(result.DirtyState, Is.Null);
            Assert.That(result.ResolvedInput, Is.Null);
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

        private static void WriteProjectFile (
            string projectRootPath,
            string projectRelativePath,
            string contents)
        {
            var absolutePath = Path.Combine(projectRootPath, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(absolutePath, contents);
        }

        private static IpcBuildProjectMutationAuditItem FindMutationItem (
            IReadOnlyList<IpcBuildProjectMutationAuditItem> items,
            string path)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Path, path, StringComparison.Ordinal))
                {
                    return items[i];
                }
            }

            throw new AssertionException($"Project mutation audit item was not found: {path}");
        }

        private static IpcBuildDirtyStateItem FindDirtyItem (
            IReadOnlyList<IpcBuildDirtyStateItem> items,
            string path)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Path, path, StringComparison.Ordinal))
                {
                    return items[i];
                }
            }

            throw new AssertionException($"Dirty state item was not found: {path}");
        }

        private static void AssertOrderedByPath (IReadOnlyList<IpcBuildProjectMutationAuditItem> items)
        {
            for (var i = 1; i < items.Count; i++)
            {
                Assert.That(
                    string.Compare(items[i - 1].Path, items[i].Path, StringComparison.Ordinal),
                    Is.LessThanOrEqualTo(0));
            }
        }

        private static void AssertOrderedByPath (IReadOnlyList<IpcBuildDirtyStateItem> items)
        {
            for (var i = 1; i < items.Count; i++)
            {
                Assert.That(
                    string.Compare(items[i - 1].Path, items[i].Path, StringComparison.Ordinal),
                    Is.LessThanOrEqualTo(0));
            }
        }

        private static UnityEditorObservation CreateSnapshot (
            DaemonEditorMode editorMode = DaemonEditorMode.Batchmode,
            long compileGeneration = 1,
            long domainReloadGeneration = 1)
        {
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: editorMode,
                    lifecycleState: IpcEditorLifecycleState.Ready,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(
                        CompileGeneration: compileGeneration,
                        DomainReloadGeneration: domainReloadGeneration,
                        AssetRefreshGeneration: 0,
                        PlayModeGeneration: 1),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero));
        }

        private sealed class BuildPreconditionDirtyAsset : ScriptableObject
        {
            public string dirtyMarker = string.Empty;
        }

        private sealed class MutableReadinessGate : IUnityEditorReadinessGate
        {
            private readonly IpcError? error;

            public MutableReadinessGate (
                UnityEditorObservation snapshot,
                IpcError? error = null)
            {
                Snapshot = snapshot;
                this.error = error;
            }

            public UnityEditorObservation Snapshot { get; set; }

            public int CaptureObservationCallCount { get; private set; }

            public UnityEditorObservation CaptureObservation ()
            {
                CaptureObservationCallCount++;
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

            public UnityBuildTargetSupportProbeResult Probe (BuildTargetStableName buildTarget)
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

            public UnityBuildTargetSupportProbeResult Probe (BuildTargetStableName buildTarget)
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

            public UnityBuildTargetSupportProbeResult Probe (BuildTargetStableName buildTarget)
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
