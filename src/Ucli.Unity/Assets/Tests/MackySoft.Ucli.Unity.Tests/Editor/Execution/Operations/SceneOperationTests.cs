using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class SceneOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_WhenScenePathIsValid_OpensTargetScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var createdScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(createdScene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: false);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_WhenSceneIsAlreadyLoaded_ReusesLoadedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: false);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_WhenAnotherDirtyLoadedSceneExists_ReturnsInvalidArgumentBeforeOpening () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var targetScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(targetScene, scenePath);
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("DirtyRoot");
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty loaded scene blocks opening scene"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenAnotherDirtyLoadedSceneExists_ReturnsInvalidArgumentBeforePlanning () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var targetScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(targetScene, scenePath);
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("DirtyRoot");
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty loaded scene blocks opening scene"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenDirtyPrefabStageExists_ReturnsInvalidArgumentBeforePlanning () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var targetScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(targetScene, scenePath);
            _ = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefabPath = scope.CreatePrefabAsset(nameof(SceneOperationTests), "PrefabRoot");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            var child = new GameObject("DirtyChild");
            child.transform.SetParent(prefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty prefab stage blocks opening scene"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenSceneIsOnlyDirtyWithoutRequestChange_SavesLoadedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneSaveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            _ = new GameObject("DirtyObject");
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(scene.isDirty, Is.True);
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.SceneSave,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(result.Persisted, Is.True);
            Assert.That(scene.isDirty, Is.False);
            Assert.That(result.ReadInvalidations.Count, Is.EqualTo(1));
            Assert.That(result.ReadInvalidations[0].Surface, Is.EqualTo(OperationReadInvalidationSurface.SceneTreeLite));
            Assert.That(result.ReadInvalidations[0].ScenePath, Is.EqualTo(scenePath.Replace('\\', '/')));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenOnlyPreviewSceneExistsWithoutPlannedLiveOpen_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneSaveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryGetOrOpenTemporaryScene(scenePath, out _, out var previewErrorMessage), Is.True, previewErrorMessage);
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.SceneSave,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertInvalidArgument(result, "op-save");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenSceneOpenWasPlannedForClosedScene_Succeeds () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var saveOperation = new SceneSaveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var saveRequest = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.SceneSave,
                args: new
                {
                    path = scenePath,
                });

            var openPlanResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            var savePlanResult = await saveOperation.PlanAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(openPlanResult, applied: false, changed: false);
            AssertSuccess(savePlanResult, applied: false, changed: false);
            Assert.That(savePlanResult.Persisted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenEditSourceScenePathIsValid_TracksPreviewScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var createdScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(createdScene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            Assert.That(previewScene.IsValid(), Is.True);
            Assert.That(previewScene.isLoaded, Is.True);
            Assert.That(EditorSceneManager.IsPreviewScene(previewScene), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenRawScenePathIsValid_DoesNotTrackPreviewScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var createdScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(createdScene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.TryGetTemporaryScene(scenePath, out _), Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.Not.EqualTo(scenePath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenLoadedSceneIsDirty_TracksPreviewSceneSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            root.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            Assert.That(previewScene.IsValid(), Is.True);
            Assert.That(previewScene.isLoaded, Is.True);
            Assert.That(EditorSceneManager.IsPreviewScene(previewScene), Is.True);
            Assert.That(previewScene, Is.Not.EqualTo(scene));
            Assert.That(
                previewScene.GetRootGameObjects(),
                Has.Some.Matches<GameObject>(gameObject => gameObject.name == "Renamed"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenLoadedSceneIsDirty_RebindsCrossRootObjectReferencesInsidePreviewScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneOpenOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rootA = new GameObject("RootA");
            var component = rootA.AddComponent<CompOperationTestComponent>();
            var rootB = new GameObject("RootB");
            var serializedObject = new SerializedObject(component);
            serializedObject.FindProperty("objectReferenceValue").objectReferenceValue = rootB;
            serializedObject.FindProperty("componentReferenceValue").objectReferenceValue = rootB.transform;
            serializedObject.FindProperty("exposedObjectReferenceValue.defaultValue").objectReferenceValue = rootB;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, scenePath);

            rootB.name = "RenamedRootB";
            EditorSceneManager.MarkSceneDirty(scene);

            var requestOperation = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            var previewRootA = FindRootGameObject(previewScene, "RootA");
            var previewRootB = FindRootGameObject(previewScene, "RenamedRootB");
            var previewComponent = previewRootA.GetComponent<CompOperationTestComponent>();
            Assert.That(previewComponent, Is.Not.Null);
            Assert.That(previewComponent!.ObjectReferenceValue, Is.SameAs(previewRootB));
            Assert.That(previewComponent.ObjectReferenceValue, Is.Not.SameAs(rootB));
            Assert.That(previewComponent.ComponentReferenceValue, Is.SameAs(previewRootB.transform));
            Assert.That(previewComponent.ComponentReferenceValue, Is.Not.SameAs(rootB.transform));
            Assert.That(previewComponent.ExposedObjectReferenceValue, Is.SameAs(previewRootB));
            Assert.That(previewComponent.ExposedObjectReferenceValue, Is.Not.SameAs(rootB));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenPreviewSceneIsDirty_ReturnsChangedTrue () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var createOperation = new GoCreateOperation();
            var saveOperation = new SceneSaveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);

            AssertSuccess(createResult, applied: false, changed: true);

            var saveRequest = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.SceneSave,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var saveResult = await saveOperation.PlanAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: false, changed: true);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOnlyPreviewSceneIsTracked_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var createOperation = new GoCreateOperation();
            var saveOperation = new SceneSaveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var saveRequest = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.SceneSave,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            _ = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            _ = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertInvalidArgument(saveResult, "op-save");
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Validate_WhenDepthIsNegative_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                    depth = -1,
                });

            var result = await operation.ValidateAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-tree");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Validate_WhenCursorIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                    cursor = "not-a-cursor",
                });

            var result = await operation.ValidateAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-tree");
            Assert.That(result.Failure!.Message, Does.Contain("args.cursor"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Validate_WhenLimitIsOutOfRange_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var invalidLimits = new[] { 0, BoundedWindowConstants.MaxLimit + 1 };

            for (var i = 0; i < invalidLimits.Length; i++)
            {
                var requestOperation = CreateOperation(
                    opId: "op-tree",
                    opName: UcliPrimitiveOperationNames.SceneTree,
                    args: new
                    {
                        path = scenePath,
                        limit = invalidLimits[i],
                    });

                var result = await operation.ValidateAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

                AssertInvalidArgument(result, "op-tree");
                Assert.That(result.Failure!.Message, Does.Contain("args.limit"));
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Plan_WhenDepthIsNull_AcceptsUnlimitedDepth () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                    depth = (int?)null,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Result!.Value.GetProperty("path").GetString(), Is.EqualTo(scenePath));
            Assert.That(result.Result.Value.GetProperty("roots").GetArrayLength(), Is.EqualTo(1));
            Assert.That(result.Result.Value.GetProperty("roots")[0].GetProperty("children").GetArrayLength(), Is.EqualTo(1));
            Assert.That(result.Result.Value.GetProperty("roots")[0].GetProperty("childrenState").GetString(), Is.EqualTo(IndexSceneTreeLiteNodeChildrenStateValues.Complete));
            Assert.That(result.Result.Value.GetProperty("sourceState").GetProperty("kind").GetString(), Is.EqualTo("loadedScene"));
            Assert.That(result.Result.Value.GetProperty("sourceState").GetProperty("isDirty").GetBoolean(), Is.False);
            Assert.That(result.Result.Value.GetProperty("window").GetProperty("limit").GetInt32(), Is.EqualTo(BoundedWindowConstants.DefaultLimit));
            Assert.That(result.Result.Value.GetProperty("window").TryGetProperty("after", out _), Is.False);
            Assert.That(result.Result.Value.GetProperty("window").GetProperty("totalCount").GetInt32(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Plan_WhenLimitCutsDirectChildren_MarksChildrenStateTruncatedByWindow () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var firstChild = new GameObject("FirstChild");
            var secondChild = new GameObject("SecondChild");
            firstChild.transform.SetParent(root.transform, worldPositionStays: false);
            secondChild.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                    depth = (int?)null,
                    limit = 2,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            var payload = result.Result!.Value;
            var rootElement = payload.GetProperty("roots")[0];
            Assert.That(rootElement.GetProperty("children").GetArrayLength(), Is.EqualTo(1));
            Assert.That(rootElement.GetProperty("childrenState").GetString(), Is.EqualTo(IndexSceneTreeLiteNodeChildrenStateValues.TruncatedByWindow));
            Assert.That(payload.GetProperty("window").GetProperty("nextCursor").GetString(), Is.EqualTo(BoundedWindowCursorCodec.Encode(2)));
            Assert.That(payload.GetProperty("window").GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Plan_WhenDepthStopsExpansion_MarksChildrenStateNotExpandedByDepth () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                    depth = 0,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            var rootElement = result.Result!.Value.GetProperty("roots")[0];
            Assert.That(rootElement.GetProperty("children").GetArrayLength(), Is.EqualTo(0));
            Assert.That(rootElement.GetProperty("childrenState").GetString(), Is.EqualTo(IndexSceneTreeLiteNodeChildrenStateValues.NotExpandedByDepth));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Call_WhenSceneIsLoadedDirty_ReturnsUnsavedRoots () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("SavedRoot");
            EditorSceneManager.SaveScene(scene, scenePath);
            _ = new GameObject("UnsavedRoot");
            EditorSceneManager.MarkSceneDirty(scene);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            var roots = result.Result!.Value.GetProperty("roots");
            Assert.That(roots.EnumerateArray().Select(static root => root.GetProperty("name").GetString()), Is.EquivalentTo(new[] { "SavedRoot", "UnsavedRoot" }));
            Assert.That(result.Result.Value.GetProperty("sourceState").GetProperty("kind").GetString(), Is.EqualTo("loadedScene"));
            Assert.That(result.Result.Value.GetProperty("sourceState").GetProperty("isDirty").GetBoolean(), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Call_WhenSceneIsNotLoaded_ReadsPersistedPreview () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("SavedRoot");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            var roots = result.Result!.Value.GetProperty("roots");
            Assert.That(roots.GetArrayLength(), Is.EqualTo(1));
            Assert.That(roots[0].GetProperty("name").GetString(), Is.EqualTo("SavedRoot"));
            Assert.That(result.Result.Value.GetProperty("sourceState").GetProperty("kind").GetString(), Is.EqualTo("persistedPreview"));
            Assert.That(result.Result.Value.GetProperty("sourceState").GetProperty("isDirty").GetBoolean(), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Plan_WhenDepthIsOmitted_AcceptsUnlimitedDepth () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Tree_Plan_WhenSceneUsesMirroredLoadedState_PreservesStableGlobalObjectId () => UniTask.ToCoroutine(async () =>
        {
            var operation = new SceneTreeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(root).ToString();
            root.name = "RenamedRoot";
            EditorSceneManager.MarkSceneDirty(scene);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            var previewRoots = temporaryScene.GetRootGameObjects();
            Assert.That(previewRoots, Has.Length.EqualTo(1));
            Assert.That(previewRoots[0].name, Is.EqualTo("RenamedRoot"));
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewRoots[0], out _), Is.False);
            var requestOperation = CreateOperation(
                opId: "op-tree",
                opName: UcliPrimitiveOperationNames.SceneTree,
                args: new
                {
                    path = scenePath,
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            var roots = result.Result!.Value.GetProperty("roots");
            Assert.That(roots.GetArrayLength(), Is.EqualTo(1));
            Assert.That(roots[0].GetProperty("name").GetString(), Is.EqualTo("RenamedRoot"));
            Assert.That(roots[0].GetProperty("globalObjectId").GetString(), Is.EqualTo(expectedGlobalObjectId));
            Assert.That(result.Result.Value.GetProperty("sourceState").GetProperty("kind").GetString(), Is.EqualTo("temporaryScene"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_Plan_ReturnsMatchesInHierarchyTraversalOrder () => UniTask.ToCoroutine(async () =>
        {
            var queryOperation = new SceneQueryOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var zRoot = new GameObject("ZRoot");
            var zChild = new GameObject("ZChild");
            zChild.transform.SetParent(zRoot.transform, worldPositionStays: false);
            var aChild = new GameObject("AChild");
            aChild.transform.SetParent(zRoot.transform, worldPositionStays: false);
            _ = new GameObject("ARoot");
            EditorSceneManager.SaveScene(scene, scenePath);
            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                });

            var queryResult = await queryOperation.PlanAsync(queryRequest, scope.CreateExecutionContext(), CancellationToken.None);

            Assert.That(queryResult.IsSuccess, Is.True);
            Assert.That(queryResult.Applied, Is.False);
            Assert.That(queryResult.Changed, Is.False);
            Assert.That(queryResult.Failure, Is.Null);
            Assert.That(queryResult.Result.HasValue, Is.True);
            var matches = queryResult.Result!.Value.GetProperty("matches");
            Assert.That(matches.GetArrayLength(), Is.EqualTo(4));
            Assert.That(matches[0].GetProperty("hierarchyPath").GetString(), Is.EqualTo("ZRoot"));
            Assert.That(matches[1].GetProperty("hierarchyPath").GetString(), Is.EqualTo("ZRoot/ZChild"));
            Assert.That(matches[2].GetProperty("hierarchyPath").GetString(), Is.EqualTo("ZRoot/AChild"));
            Assert.That(matches[3].GetProperty("hierarchyPath").GetString(), Is.EqualTo("ARoot"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_Plan_WhenPreviewSceneHasPlannedChanges_UsesPreviewSceneState () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var createOperation = new GoCreateOperation();
            var queryOperation = new SceneQueryOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);

            AssertSuccess(createResult, applied: false, changed: true);

            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                    pathPrefix = "CreatedRoot",
                });
            var queryResult = await queryOperation.PlanAsync(queryRequest, context, CancellationToken.None);

            AssertSuccess(queryResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(queryResult.Result.HasValue, Is.True);
            Assert.That(queryResult.Result!.Value.GetProperty("scene").GetString(), Is.EqualTo(scenePath));
            Assert.That(queryResult.Result.Value.GetProperty("matches").GetArrayLength(), Is.EqualTo(1));
            Assert.That(queryResult.Result.Value.GetProperty("matches")[0].GetProperty("hierarchyPath").GetString(), Is.EqualTo("CreatedRoot"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_Plan_WhenPreviewSceneHasEnsuredComponent_IncludesEnsuredComponentMatch () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var ensureOperation = new CompEnsureOperation();
            var queryOperation = new SceneQueryOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();

            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            AssertSuccess(openResult, applied: false, changed: false);

            var componentTypeId = MackySoft.Ucli.Unity.Index.IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var ensureRequest = CreateOperation(
                opId: "op-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    type = componentTypeId,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var ensureResult = await ensureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);
            AssertSuccess(ensureResult, applied: false, changed: true);

            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                    pathPrefix = "Root",
                    componentType = componentTypeId,
                });
            var queryResult = await queryOperation.PlanAsync(queryRequest, context, CancellationToken.None);

            AssertSuccess(queryResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(queryResult.Result.HasValue, Is.True);
            var matches = queryResult.Result!.Value.GetProperty("matches");
            Assert.That(matches.GetArrayLength(), Is.EqualTo(1));
            Assert.That(matches[0].GetProperty("kind").GetString(), Is.EqualTo("component"));
            Assert.That(matches[0].GetProperty("hierarchyPath").GetString(), Is.EqualTo("Root"));
            Assert.That(matches[0].GetProperty("componentType").GetString(), Is.EqualTo(componentTypeId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_Plan_WhenSceneIsClosed_DoesNotPrepareTrackedPreviewState () => UniTask.ToCoroutine(async () =>
        {
            var queryOperation = new SceneQueryOperation();
            var createOperation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                    pathPrefix = "Root",
                });

            var queryResult = await queryOperation.PlanAsync(queryRequest, context, CancellationToken.None);

            AssertSuccess(queryResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(context.TryGetTemporaryScene(scenePath, out _), Is.False);

            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedAfterQuery",
                    scene = scenePath,
                });
            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);

            AssertInvalidArgument(createResult, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_PlanAndCall_WhenLoadedSceneIsDirty_UseLoadedSceneState () => UniTask.ToCoroutine(async () =>
        {
            var queryOperation = new SceneQueryOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);

            _ = new GameObject("DirtyRoot");
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(scene.isDirty, Is.True);

            var context = scope.CreateExecutionContext();
            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                    pathPrefix = "DirtyRoot",
                });

            var planResult = await queryOperation.PlanAsync(queryRequest, context, CancellationToken.None);
            var callResult = await queryOperation.CallAsync(queryRequest, context, CancellationToken.None);

            AssertSuccess(planResult, applied: false, changed: false, expectedTouchKind: null);
            AssertSuccess(callResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(context.TryGetTemporaryScene(scenePath, out _), Is.False);
            Assert.That(planResult.Result.HasValue, Is.True);
            Assert.That(callResult.Result.HasValue, Is.True);
            Assert.That(planResult.Result!.Value.GetProperty("matches").GetArrayLength(), Is.EqualTo(1));
            Assert.That(callResult.Result!.Value.GetProperty("matches").GetArrayLength(), Is.EqualTo(1));
            Assert.That(planResult.Result.Value.GetProperty("matches")[0].GetProperty("hierarchyPath").GetString(), Is.EqualTo("DirtyRoot"));
            Assert.That(callResult.Result.Value.GetProperty("matches")[0].GetProperty("hierarchyPath").GetString(), Is.EqualTo("DirtyRoot"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_Plan_WhenHierarchyPathIsAmbiguous_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var queryOperation = new SceneQueryOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var duplicateA = new GameObject("Dup");
            duplicateA.transform.SetParent(root.transform, worldPositionStays: false);
            var duplicateB = new GameObject("Dup");
            duplicateB.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                    pathPrefix = "Root/Dup",
                });

            var queryResult = await queryOperation.PlanAsync(queryRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(queryResult, "op-query");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_Plan_WhenSlashNameDiagnosticPrecedesFailure_PreservesDiagnostic () => UniTask.ToCoroutine(async () =>
        {
            var queryOperation = new SceneQueryOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Bad/Root");
            var root = new GameObject("Root");
            var duplicateA = new GameObject("Dup");
            duplicateA.transform.SetParent(root.transform, worldPositionStays: false);
            var duplicateB = new GameObject("Dup");
            duplicateB.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                    pathPrefix = "Root/Dup",
                });

            var queryResult = await queryOperation.PlanAsync(queryRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(queryResult, "op-query");
            var diagnostic = AssertSingleHierarchyPathDiagnostic(queryResult.Diagnostics);
            Assert.That(diagnostic.Severity, Is.EqualTo(IpcExecuteDiagnosticSeverityNames.Warning));
            Assert.That(diagnostic.CoverageImpact, Is.EqualTo(IpcExecuteDiagnosticCoverageImpactNames.Partial));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Query_Plan_WhenUnrelatedHierarchyContainsSlashInName_IgnoresThatSubtree () => UniTask.ToCoroutine(async () =>
        {
            var queryOperation = new SceneQueryOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(SceneOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("GoodRoot");
            _ = new GameObject("Bad/Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var queryRequest = CreateOperation(
                opId: "op-query",
                opName: UcliPrimitiveOperationNames.SceneQuery,
                args: new
                {
                    scene = scenePath,
                    pathPrefix = "GoodRoot",
                });

            var queryResult = await queryOperation.PlanAsync(queryRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(queryResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(queryResult.Result.HasValue, Is.True);
            var matches = queryResult.Result!.Value.GetProperty("matches");
            Assert.That(matches.GetArrayLength(), Is.EqualTo(1));
            Assert.That(matches[0].GetProperty("hierarchyPath").GetString(), Is.EqualTo("GoodRoot"));
            var diagnostic = AssertSingleHierarchyPathDiagnostic(queryResult.Diagnostics);
            Assert.That(diagnostic.Severity, Is.EqualTo(IpcExecuteDiagnosticSeverityNames.Warning));
            Assert.That(diagnostic.CoverageImpact, Is.EqualTo(IpcExecuteDiagnosticCoverageImpactNames.Partial));
        });

        private static NormalizedOperation CreateOperation (
            string opId,
            string opName,
            object args,
            NormalizedOperation.SourceStepKind sourceKind = NormalizedOperation.SourceStepKind.Op)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: null,
                Expect: null,
                SourceKind: sourceKind);
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
            bool changed,
            OperationTouchKind? expectedTouchKind = OperationTouchKind.Scene)
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            if (!expectedTouchKind.HasValue)
            {
                Assert.That(result.Touched, Is.Empty);
                Assert.That(result.Failure, Is.Null);
                return;
            }

            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(expectedTouchKind.Value));
            Assert.That(result.Failure, Is.Null);
        }

        private static OperationDiagnostic AssertSingleHierarchyPathDiagnostic (IReadOnlyList<OperationDiagnostic> diagnostics)
        {
            Assert.That(diagnostics.Count, Is.EqualTo(1));
            var diagnostic = diagnostics[0];
            Assert.That(diagnostic.Code, Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects));
            Assert.That(diagnostic.Message, Does.Contain("hierarchyPath cannot represent"));
            return diagnostic;
        }

        private static GameObject FindRootGameObject (
            Scene scene,
            string name)
        {
            var rootGameObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootGameObjects.Length; i++)
            {
                if (rootGameObjects[i].name == name)
                {
                    return rootGameObjects[i];
                }
            }

            Assert.Fail($"Root GameObject was not found: {name}.");
            return null!;
        }
    }
}
