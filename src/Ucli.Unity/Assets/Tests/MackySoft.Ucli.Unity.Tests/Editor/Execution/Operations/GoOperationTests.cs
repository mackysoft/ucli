using System;
using MackySoft.Ucli.Contracts;
using System.Collections;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
    public sealed class GoOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenScenePathIsValid_CreatesRootGameObjectAndStoresAlias () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                },
                alias: "created");
            var context = scope.CreateExecutionContext();

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            var loadedScene = SceneManager.GetSceneByPath(scenePath);
            Assert.That(loadedScene.IsValid(), Is.True);
            Assert.That(loadedScene.isLoaded, Is.True);
            Assert.That(loadedScene.GetRootGameObjects().Any(static gameObject => gameObject.name == "CreatedRoot"), Is.True);
            Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.SceneTreeLite, scenePath.Replace('\\', '/')));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenParentUsesAlias_CreatesChildUnderParent () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parent = new GameObject("Parent");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            context.AliasStore.Set("parent", UnityObjectReferenceResolver.CreateResolvedReference(parent));
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedChild",
                    parent = new
                    {
                        @var = "parent",
                    },
                },
                alias: "created");

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(parent.transform.childCount, Is.EqualTo(1));
            Assert.That(parent.transform.GetChild(0).name, Is.EqualTo("CreatedChild"));
            Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenParentUsesSelector_CreatesChildUnderParent () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parent = new GameObject("Parent");
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedChild",
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Parent",
                    },
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(parent.transform.childCount, Is.EqualTo(1));
            Assert.That(parent.transform.GetChild(0).name, Is.EqualTo("CreatedChild"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_Call_WhenSceneObjectIsValid_DeletesObjectAndEmitsSceneTreeLiteInvalidation () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDeleteOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-delete",
                opName: UcliPrimitiveOperationNames.GoDelete,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                });

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(root.transform.childCount, Is.EqualTo(0));
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.True);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.SceneTreeLite, scenePath.Replace('\\', '/')));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Reparent_Call_WhenSceneObjectMoves_EmitsSceneTreeLiteInvalidation () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoReparentOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var container = new GameObject("Container");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-reparent",
                opName: UcliPrimitiveOperationNames.GoReparent,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Container",
                    },
                });

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(child.transform.parent, Is.SameAs(container.transform));
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.True);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.SceneTreeLite, scenePath.Replace('\\', '/')));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenAliasIsSpecified_StoresTemporaryAlias () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
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
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                },
                alias: "created");

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(context.TryGetTemporaryAliasState("created", out var temporaryAliasState), Is.True);
            Assert.That(context.AliasStore.TryGet("created", out _), Is.False);
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(scenePath));
            Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<GameObject>());
            Assert.That(((GameObject)temporaryAliasState.UnityObject!).name, Is.EqualTo("CreatedRoot"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenSceneAndParentAreBothSpecified_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = "Assets/Scenes/Main.unity",
                    parent = new
                    {
                        @var = "parent",
                    },
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenSceneAndParentAreBothMissing_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenParentResolvesAsset_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            _ = scope.CreateScriptableAsset<IndexCatalogTestAsset>(nameof(GoOperationTests), out var assetPath);
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedChild",
                    parent = new
                    {
                        assetPath,
                    },
                });

            var result = await operation.ValidateAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenTargetUsesAlias_ReturnsSuccess () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            root.AddComponent<BoxCollider>();
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(root));
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        @var = "target",
                    },
                    depth = 1,
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenTargetUsesSelector_ReturnsSuccess () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Result!.Value.GetProperty("name").GetString(), Is.EqualTo("Child"));
            Assert.That(result.Result.Value.GetProperty("children").GetArrayLength(), Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenTargetHasEnsuredComponent_IncludesEnsuredComponent () => UniTask.ToCoroutine(async () =>
        {
            var ensureOperation = new CompEnsureOperation();
            var describeOperation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();

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
                    type = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                });
            var ensureResult = await ensureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);
            AssertSuccess(ensureResult, applied: false, changed: true);

            var describeRequest = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                });

            var describeResult = await describeOperation.PlanAsync(describeRequest, context, CancellationToken.None);

            AssertSuccess(describeResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(describeResult.Result.HasValue, Is.True);
            var components = describeResult.Result!.Value.GetProperty("components").EnumerateArray()
                .Select(element => element.GetProperty("typeName").GetString())
                .ToArray();
            Assert.That(components, Does.Contain(typeof(BoxCollider).FullName));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenTargetUsesMirroredPrefabObject_DoesNotInventStableGlobalObjectId () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(GoOperationTests), "PrefabRoot", "Child");

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = prefabStage!.prefabContentsRoot.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);
            stageChild!.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChild = temporaryRoot!.transform.Find("Renamed");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild!.gameObject, out _), Is.False);
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        prefab = prefabPath,
                        hierarchyPath = $"{temporaryRoot.name}/Renamed",
                    },
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Result!.Value.GetProperty("globalObjectId").GetString(), Is.EqualTo(string.Empty));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenPreviewSceneChildWasRecreated_DoesNotReuseStableGlobalObjectId () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            UnityEngine.Object.DestroyImmediate(child);
            var recreatedChild = new GameObject("Child");
            recreatedChild.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(scene);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            var temporaryRoot = temporaryScene.GetRootGameObjects().Single(static gameObject => gameObject.name == "Root");
            var previewChild = temporaryRoot.transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild!.gameObject, out _), Is.False);
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            var describedGlobalObjectId = result.Result!.Value.GetProperty("globalObjectId").GetString();
            if (UnityObjectReferenceResolver.TryCreateResolvedReference(recreatedChild, out var recreatedReference))
            {
                Assert.That(describedGlobalObjectId, Is.EqualTo(recreatedReference!.GlobalObjectId));
            }
            else
            {
                Assert.That(describedGlobalObjectId, Is.EqualTo(string.Empty));
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenPreviewPrefabChildWasRecreated_DoesNotReuseStableGlobalObjectId () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(GoOperationTests), "PrefabRoot", "Child");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);
            var stageChild = stageRoot!.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);

            UnityEngine.Object.DestroyImmediate(stageChild!.gameObject);
            var recreatedChild = new GameObject("Child");
            recreatedChild.transform.SetParent(stageRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChild = temporaryRoot!.transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild!.gameObject, out _), Is.False);
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        prefab = prefabPath,
                        hierarchyPath = $"{temporaryRoot.name}/Child",
                    },
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Result!.Value.GetProperty("globalObjectId").GetString(), Is.EqualTo(string.Empty));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Validate_WhenDepthIsNegative_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        @var = "target",
                    },
                    depth = -1,
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-describe");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Validate_WhenTargetResolvesAsset_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            _ = scope.CreateScriptableAsset<IndexCatalogTestAsset>(nameof(GoOperationTests), out var assetPath);
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                });

            var result = await operation.ValidateAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-describe");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenSceneIsClosedWithoutPriorOpen_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenSceneIsLoadedWithoutPriorOpen_UsesRequestLocalPlanState () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            var loadedScene = SceneManager.GetSceneByPath(scenePath);
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(loadedScene.GetRootGameObjects().Any(static gameObject => gameObject.name == "CreatedRoot"), Is.False);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            Assert.That(temporaryScene.GetRootGameObjects().Any(static gameObject => gameObject.name == "CreatedRoot"), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenParentIsLiveSceneObjectWithoutPriorOpen_UsesRequestLocalPlanState () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parent = new GameObject("Parent");
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedChild",
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = parent.name,
                    },
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: true);
            Assert.That(parent.transform.childCount, Is.EqualTo(0));
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            var temporaryParent = temporaryScene.GetRootGameObjects().Single(static gameObject => gameObject.name == "Parent");
            Assert.That(temporaryParent.transform.childCount, Is.EqualTo(1));
            Assert.That(temporaryParent.transform.GetChild(0).name, Is.EqualTo("CreatedChild"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenOnlyPreviewSceneIsTracked_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var createOperation = new GoCreateOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
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
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                });

            var createResult = await createOperation.CallAsync(createRequest, context, CancellationToken.None);

            AssertInvalidArgument(createResult, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_Plan_WhenExistingSceneObjectIsDeleted_FollowupDescribeFails () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var deleteOperation = new GoDeleteOperation();
            var describeOperation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var deleteRequest = CreateOperation(
                opId: "op-delete",
                opName: UcliPrimitiveOperationNames.GoDelete,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                });
            var deleteResult = await deleteOperation.PlanAsync(deleteRequest, context, CancellationToken.None);

            AssertSuccess(deleteResult, applied: false, changed: true);

            var describeRequest = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                });

            var describeResult = await describeOperation.PlanAsync(describeRequest, context, CancellationToken.None);

            AssertInvalidArgument(describeResult, "op-describe");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_Plan_WhenTargetIsLiveSceneObjectWithoutPriorOpen_UsesRequestLocalPlanState () => UniTask.ToCoroutine(async () =>
        {
            var deleteOperation = new GoDeleteOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var deleteRequest = CreateOperation(
                opId: "op-delete",
                opName: UcliPrimitiveOperationNames.GoDelete,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                });
            var context = scope.CreateExecutionContext();

            var deleteResult = await deleteOperation.PlanAsync(deleteRequest, context, CancellationToken.None);

            AssertSuccess(deleteResult, applied: false, changed: true);
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            var temporaryRoot = temporaryScene.GetRootGameObjects().Single(static gameObject => gameObject.name == "Root");
            Assert.That(temporaryRoot.transform.childCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_Call_WhenOnlyPreviewSceneIsTracked_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var deleteOperation = new GoDeleteOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
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
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var deleteRequest = CreateOperation(
                opId: "op-delete",
                opName: UcliPrimitiveOperationNames.GoDelete,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                });

            var deleteResult = await deleteOperation.CallAsync(deleteRequest, context, CancellationToken.None);

            AssertInvalidArgument(deleteResult, "op-delete");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_Plan_WhenTargetIsPrefabRoot_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var deleteOperation = new GoDeleteOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(GoOperationTests), "PrefabRoot", "Child");
            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            Assert.That(openResult.IsSuccess, Is.True);

            var deleteRequest = CreateOperation(
                opId: "op-delete",
                opName: UcliPrimitiveOperationNames.GoDelete,
                args: new
                {
                    target = new
                    {
                        prefab = prefabPath,
                        hierarchyPath = prefabRootName,
                    },
                });

            var deleteResult = await deleteOperation.PlanAsync(deleteRequest, context, CancellationToken.None);

            AssertInvalidArgument(deleteResult, "op-delete");
            Assert.That(deleteResult.Failure!.Message, Does.Contain("Prefab root cannot be deleted."));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_Call_WhenTargetIsPrefabRoot_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var deleteOperation = new GoDeleteOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(GoOperationTests), "PrefabRoot", "Child");
            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                });
            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);

            Assert.That(openResult.IsSuccess, Is.True);

            var deleteRequest = CreateOperation(
                opId: "op-delete",
                opName: UcliPrimitiveOperationNames.GoDelete,
                args: new
                {
                    target = new
                    {
                        prefab = prefabPath,
                        hierarchyPath = prefabRootName,
                    },
                });

            var deleteResult = await deleteOperation.CallAsync(deleteRequest, context, CancellationToken.None);

            AssertInvalidArgument(deleteResult, "op-delete");
            Assert.That(deleteResult.Failure!.Message, Does.Contain("Prefab root cannot be deleted."));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Reparent_Plan_WhenLiveSceneObjectsAreUsedWithoutPriorOpen_UsesRequestLocalPlanState () => UniTask.ToCoroutine(async () =>
        {
            var reparentOperation = new GoReparentOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            _ = new GameObject("Container");
            EditorSceneManager.SaveScene(scene, scenePath);
            var reparentRequest = CreateOperation(
                opId: "op-reparent",
                opName: UcliPrimitiveOperationNames.GoReparent,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Container",
                    },
                });
            var context = scope.CreateExecutionContext();

            var reparentResult = await reparentOperation.PlanAsync(reparentRequest, context, CancellationToken.None);

            AssertSuccess(reparentResult, applied: false, changed: true);
            Assert.That(child.transform.parent, Is.SameAs(root.transform));
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            var temporaryContainer = temporaryScene.GetRootGameObjects().Single(static gameObject => gameObject.name == "Container");
            Assert.That(temporaryContainer.transform.childCount, Is.EqualTo(1));
            Assert.That(temporaryContainer.transform.GetChild(0).name, Is.EqualTo("Child"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Reparent_Plan_WhenExistingSceneObjectMoves_NewPathSucceedsAndOldPathFails () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var reparentOperation = new GoReparentOperation();
            var describeOperation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var container = new GameObject("Container");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var reparentRequest = CreateOperation(
                opId: "op-reparent",
                opName: UcliPrimitiveOperationNames.GoReparent,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Container",
                    },
                });

            var reparentResult = await reparentOperation.PlanAsync(reparentRequest, context, CancellationToken.None);

            AssertSuccess(reparentResult, applied: false, changed: true);

            var newPathDescribe = CreateOperation(
                opId: "op-describe-new",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Container/Child",
                    },
                });
            var newPathResult = await describeOperation.PlanAsync(newPathDescribe, context, CancellationToken.None);

            AssertSuccess(newPathResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(newPathResult.Result.HasValue, Is.True);
            Assert.That(newPathResult.Result!.Value.GetProperty("name").GetString(), Is.EqualTo("Child"));

            var oldPathDescribe = CreateOperation(
                opId: "op-describe-old",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                });
            var oldPathResult = await describeOperation.PlanAsync(oldPathDescribe, context, CancellationToken.None);

            AssertInvalidArgument(oldPathResult, "op-describe-old");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Reparent_Plan_WhenTargetAlreadyUsesSpecifiedParent_ReturnsNoChange () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var reparentOperation = new GoReparentOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-open",
                opName: UcliPrimitiveOperationNames.SceneOpen,
                args: new
                {
                    path = scenePath,
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var reparentRequest = CreateOperation(
                opId: "op-reparent",
                opName: UcliPrimitiveOperationNames.GoReparent,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                });

            var reparentResult = await reparentOperation.PlanAsync(reparentRequest, context, CancellationToken.None);

            Assert.That(reparentResult.IsSuccess, Is.True);
            Assert.That(reparentResult.Applied, Is.False);
            Assert.That(reparentResult.Changed, Is.False);
            Assert.That(reparentResult.Touched, Is.Empty);
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Reparent_Call_WhenTargetAlreadyUsesSpecifiedParent_ReturnsNoChange () => UniTask.ToCoroutine(async () =>
        {
            var reparentOperation = new GoReparentOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            var reparentRequest = CreateOperation(
                opId: "op-reparent",
                opName: UcliPrimitiveOperationNames.GoReparent,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root/Child",
                    },
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                });

            var reparentResult = await reparentOperation.CallAsync(reparentRequest, context, CancellationToken.None);

            Assert.That(reparentResult.IsSuccess, Is.True);
            Assert.That(reparentResult.Applied, Is.True);
            Assert.That(reparentResult.Changed, Is.False);
            Assert.That(reparentResult.Touched, Is.Empty);
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Scene, scenePath)), Is.False);
            Assert.That(child.transform.parent, Is.SameAs(root.transform));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenAliasIsOmitted_FollowupDescribeUsesPreviewSceneState () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new SceneOpenOperation();
            var createOperation = new GoCreateOperation();
            var describeOperation = new GoDescribeOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
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
                });
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                });

            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);

            AssertSuccess(createResult, applied: false, changed: true);

            var describeRequest = CreateOperation(
                opId: "op-describe",
                opName: UcliPrimitiveOperationNames.GoDescribe,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "CreatedRoot",
                    },
                });
            var describeResult = await describeOperation.PlanAsync(describeRequest, context, CancellationToken.None);

            AssertSuccess(describeResult, applied: false, changed: false, expectedTouchKind: null);
            Assert.That(describeResult.Result.HasValue, Is.True);
            Assert.That(describeResult.Result!.Value.GetProperty("name").GetString(), Is.EqualTo("CreatedRoot"));
        });

        [Test]
        [Category("Size.Small")]
        public void DescriptionBuilder_Build_WhenDepthIsOne_CapturesComponentsAndChildren ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(GoOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            _ = root.AddComponent<BoxCollider>();
            var child = new GameObject("Child");
            _ = child.AddComponent<SphereCollider>();
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);

            var description = GameObjectDescriptionBuilder.Build(root, depth: 1);

            Assert.That(description.Name, Is.EqualTo("Root"));
            Assert.That(description.Components.Select(static component => component.TypeName), Does.Contain(typeof(Transform).FullName));
            Assert.That(description.Components.Select(static component => component.TypeName), Does.Contain(typeof(BoxCollider).FullName));
            Assert.That(description.Children.Count, Is.EqualTo(1));
            Assert.That(description.Children[0].Name, Is.EqualTo("Child"));
            Assert.That(description.Children[0].Components.Select(static component => component.TypeName), Does.Contain(typeof(SphereCollider).FullName));
            Assert.That(description.Children[0].Children.Count, Is.EqualTo(0));
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            string opName,
            object args,
            string? alias = null)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: alias,
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
