using System;
using MackySoft.Ucli.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Project;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ProjectPhaseOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Refresh_Validate_WhenArgsContainUnknownProperty_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectRefreshPhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: UcliPrimitiveOperationNames.ProjectRefresh,
                args: new
                {
                    unexpected = true,
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-refresh");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Refresh_Plan_WhenArgsAreEmpty_ReturnsNoTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectRefreshPhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: UcliPrimitiveOperationNames.ProjectRefresh,
                args: new { });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.PlanAsync(requestOperation, executionContext, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(result.Touched, Is.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Refresh_Call_WhenExternalAssetIsCreated_ImportsAssetAndReturnsTouchedAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectRefreshPhaseOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(ProjectPhaseOperationTests), ".txt");
            var absoluteAssetPath = ToAbsolutePath(assetPath);
            var assetDirectoryPath = Path.GetDirectoryName(absoluteAssetPath);
            if (!string.IsNullOrWhiteSpace(assetDirectoryPath))
            {
                Directory.CreateDirectory(assetDirectoryPath);
            }

            File.WriteAllText(absoluteAssetPath, "refresh-test");
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: UcliPrimitiveOperationNames.ProjectRefresh,
                args: new { });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(result.Persisted, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath), Is.Not.Null);
            Assert.That(result.Touched.Any(touched => touched.Path == assetPath && touched.Kind == OperationTouchKind.Asset), Is.True);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.AssetSearch, null),
                (OperationReadInvalidationSurface.GuidPath, null));
        });

        [Test]
        [Category("Size.Small")]
        public void SyncDirtyStateChanges_WhenSceneTransitionsToDirty_MarksRequestAttributedChangeAndTouchesScene ()
        {
            var scenePath = "Assets/ProjectPhaseOperationTests_Scene.unity";
            using var executionContext = new OperationExecutionContext();
            var touched = new List<OperationTouch>();

            ProjectOperationUtilities.SyncDirtyStateChanges(
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [scenePath] = false,
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [scenePath] = true,
                },
                OperationTouchKind.Scene,
                touched,
                executionContext);

            var resource = new OperationResource(OperationTouchKind.Scene, scenePath);
            Assert.That(executionContext.HasRequestAttributedChange(resource), Is.True);
            Assert.That(touched, Has.Count.EqualTo(1));
            Assert.That(touched[0].Kind, Is.EqualTo(OperationTouchKind.Scene));
            Assert.That(touched[0].Path, Is.EqualTo(scenePath));
        }

        [Test]
        [Category("Size.Small")]
        public void SyncDirtyStateChanges_WhenPrefabTransitionsToClean_ClearsRequestAttributedChangeAndTouchesPrefab ()
        {
            var prefabPath = "Assets/ProjectPhaseOperationTests_Prefab.prefab";
            using var executionContext = new OperationExecutionContext();
            var resource = new OperationResource(OperationTouchKind.Prefab, prefabPath);
            executionContext.MarkRequestAttributedChange(resource);
            var touched = new List<OperationTouch>();

            ProjectOperationUtilities.SyncDirtyStateChanges(
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [prefabPath] = true,
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [prefabPath] = false,
                },
                OperationTouchKind.Prefab,
                touched,
                executionContext);

            Assert.That(executionContext.HasRequestAttributedChange(resource), Is.False);
            Assert.That(touched, Has.Count.EqualTo(1));
            Assert.That(touched[0].Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(touched[0].Path, Is.EqualTo(prefabPath));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenArgsAreEmpty_ReturnsNoTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.PlanAsync(requestOperation, executionContext, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(result.Touched, Is.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenRequestAttributedSceneExists_ReturnsTouchedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ProjectPhaseOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            context.MarkRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath));
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene && touched.Path == scenePath), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenRequestAttributedSceneHasPlannedLiveOpen_ReturnsTouchedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ProjectPhaseOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var resource = new OperationResource(OperationTouchKind.Scene, scenePath);
            context.MarkRequestAttributedChange(resource);
            context.TrackPlannedLiveSceneOpen(scenePath);
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene && touched.Path == scenePath), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenRequestAttributedOpenedPrefabStageExists_ReturnsTouchedPrefab () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ProjectPhaseOperationTests), "PrefabRoot");
            _ = PrefabStageUtility.OpenPrefab(prefabPath);
            var context = scope.CreateExecutionContext();
            context.MarkRequestAttributedChange(new OperationResource(OperationTouchKind.Prefab, prefabPath));
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Prefab && touched.Path == prefabPath), Is.True);
        });

        [Test]
        [Category("Size.Small")]
        public async Task Save_Plan_WhenRequestAttributedPrefabHasPlannedLiveOpen_ReturnsTouchedPrefab ()
        {
            var operation = new ProjectSavePhaseOperation();
            using var executionContext = new OperationExecutionContext();
            const string prefabPath = "Assets/Generated/Planned.prefab";
            var resource = new OperationResource(OperationTouchKind.Prefab, prefabPath);
            executionContext.MarkRequestAttributedChange(resource);
            executionContext.TrackPlannedLivePrefabOpen(prefabPath);
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.PlanAsync(requestOperation, executionContext, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Prefab && touched.Path == prefabPath), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Save_Plan_WhenRequestAttributedAssetExists_ReturnsTouchedAsset ()
        {
            var operation = new ProjectSavePhaseOperation();
            using var executionContext = new OperationExecutionContext();
            const string assetPath = "Assets/Generated/Config.asset";
            executionContext.MarkRequestAttributedChange(new OperationResource(OperationTouchKind.Asset, assetPath));
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.PlanAsync(requestOperation, executionContext, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Asset && touched.Path == assetPath), Is.True);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenAssetCreatePlanRan_ReturnsTouchedAsset () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            var saveOperation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var executionContext = scope.CreateExecutionContext();
            var assetPath = scope.CreateAssetPath(nameof(ProjectPhaseOperationTests), ".asset");
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(IndexCatalogTestAsset)),
                    path = assetPath,
                });

            var createResult = await createOperation.PlanAsync(createRequest, executionContext, CancellationToken.None);

            AssertSuccess(createResult, applied: false, changed: true);
            var saveRequest = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });
            var saveResult = await saveOperation.PlanAsync(saveRequest, executionContext, CancellationToken.None);

            AssertSuccess(saveResult, applied: false, changed: true);
            Assert.That(saveResult.Touched.Any(touched => touched.Kind == OperationTouchKind.Asset && touched.Path == assetPath), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenAssetSetPlanChanged_ReturnsTouchedAsset () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new AssetSetOperation();
            var saveOperation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<IndexCatalogTestAsset>(nameof(ProjectPhaseOperationTests), out var assetPath);
            AssetDatabase.SaveAssets();
            var executionContext = scope.CreateExecutionContext();
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "speed",
                            value = 42.0f,
                        },
                    },
                });

            var setResult = await setOperation.PlanAsync(setRequest, executionContext, CancellationToken.None);

            AssertSuccess(setResult, applied: false, changed: true);
            var saveRequest = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });
            var saveResult = await saveOperation.PlanAsync(saveRequest, executionContext, CancellationToken.None);

            AssertSuccess(saveResult, applied: false, changed: true);
            Assert.That(saveResult.Touched.Any(touched => touched.Kind == OperationTouchKind.Asset && touched.Path == assetPath), Is.True);
            var serializedObject = new SerializedObject(asset);
            Assert.That(serializedObject.FindProperty("speed").floatValue, Is.Not.EqualTo(42.0f));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenPrefabCreatePlanRan_ReturnsTouchedSourceScene () => UniTask.ToCoroutine(async () =>
        {
            var prefabCreateOperation = new PrefabCreateOperation();
            var saveOperation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ProjectPhaseOperationTests));
            var prefabPath = scope.CreatePrefabPath(nameof(ProjectPhaseOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            var prefabCreateRequest = CreateOperation(
                opId: "op-prefab-create",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    path = prefabPath,
                });
            var saveRequest = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var prefabCreateResult = await prefabCreateOperation.PlanAsync(prefabCreateRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.PlanAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(prefabCreateResult, applied: false, changed: true);
            AssertSuccess(saveResult, applied: false, changed: true);
            Assert.That(saveResult.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene && touched.Path == scenePath), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenScriptableObjectAssetIsDirty_SavesAssetAndReturnsTouchedAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<IndexCatalogTestAsset>(nameof(ProjectPhaseOperationTests), out var assetPath);
            AssetDatabase.SaveAssets();

            var serializedObject = new SerializedObject(asset);
            serializedObject.FindProperty("speed").floatValue = 42.0f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            Assert.That(EditorUtility.IsDirty(asset), Is.True);

            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(result.Persisted, Is.True);
            Assert.That(EditorUtility.IsDirty(asset), Is.False);
            Assert.That(result.Touched.Any(touched => touched.Path == assetPath && touched.Kind == OperationTouchKind.Asset), Is.True);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.AssetSearch, null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOnlySceneIsDirty_DoesNotSaveScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ProjectPhaseOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            _ = new GameObject("DirtySceneObject");
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(scene.isDirty, Is.True);
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: false);
            Assert.That(result.Persisted, Is.False);
            Assert.That(scene.isDirty, Is.True);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenRequestAttributedSceneIsDirty_SavesSceneAndReturnsTouchedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ProjectPhaseOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            _ = new GameObject("DirtySceneObject");
            EditorSceneManager.MarkSceneDirty(scene);
            var context = scope.CreateExecutionContext();
            context.MarkRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath));
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(result.Persisted, Is.True);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene && touched.Path == scenePath), Is.True);
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.False);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.SceneTreeLite, scenePath.Replace('\\', '/')));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenPrefabCreateCallRan_SavesSourceSceneAndReturnsTouchedScene () => UniTask.ToCoroutine(async () =>
        {
            var prefabCreateOperation = new PrefabCreateOperation();
            var saveOperation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ProjectPhaseOperationTests));
            var prefabPath = scope.CreatePrefabPath(nameof(ProjectPhaseOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            var prefabCreateRequest = CreateOperation(
                opId: "op-prefab-create",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    path = prefabPath,
                });
            var saveRequest = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var prefabCreateResult = await prefabCreateOperation.CallAsync(prefabCreateRequest, context, CancellationToken.None);
            AssertSuccess(prefabCreateResult, applied: true, changed: true);
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.True);
            Assert.That(prefabCreateResult.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene && touched.Path == scenePath), Is.True);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(root), Is.True);

            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            Assert.That(saveResult.Persisted, Is.True);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(saveResult.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene && touched.Path == scenePath), Is.True);
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenRequestAttributedPrefabStageIsDirty_SavesPrefabAndReturnsTouchedPrefab () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ProjectPhaseOperationTests), "PrefabRoot");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            var child = new GameObject("Child");
            child.transform.SetParent(prefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var context = scope.CreateExecutionContext();
            context.MarkRequestAttributedChange(new OperationResource(OperationTouchKind.Prefab, prefabPath));
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(result.Persisted, Is.True);
            Assert.That(prefabStage.prefabContentsRoot.scene.isDirty, Is.False);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Prefab && touched.Path == prefabPath), Is.True);
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Prefab, prefabPath)), Is.False);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.AssetSearch, null));

            scope.CloseCurrentPrefabStageIfOpen();
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(loadedPrefabContentsRoot.transform.Find("Child"), Is.Not.Null);
        });

        private static string ToAbsolutePath (string assetPath)
        {
            return Path.Combine(
                UnityProjectPathResolver.ResolveProjectRootPath(),
                PathStringNormalizer.ToPlatformSeparated(assetPath));
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            string opName,
            object args)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: null,
                Expect: null);
        }

        private static void AssertInvalidArgument (
            OperationPhaseStepResult result,
            string expectedOperationId)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Failure.OpId, Is.EqualTo(expectedOperationId));
        }

        private static void AssertSuccess (
            OperationPhaseStepResult result,
            bool applied,
            bool changed)
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Failure, Is.Null);
        }

        private static void AssertReadInvalidations (
            OperationPhaseStepResult result,
            params (OperationReadInvalidationSurface Surface, string? ScenePath)[] expectedInvalidations)
        {
            Assert.That(result.ReadInvalidations.Count, Is.EqualTo(expectedInvalidations.Length));
            for (var i = 0; i < expectedInvalidations.Length; i++)
            {
                var expectedInvalidation = expectedInvalidations[i];
                Assert.That(result.ReadInvalidations[i].Surface, Is.EqualTo(expectedInvalidation.Surface));
                Assert.That(result.ReadInvalidations[i].ScenePath, Is.EqualTo(expectedInvalidation.ScenePath));
            }
        }
    }
}
