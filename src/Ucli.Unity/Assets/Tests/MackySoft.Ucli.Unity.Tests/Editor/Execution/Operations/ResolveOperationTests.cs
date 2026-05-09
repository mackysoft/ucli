using System;
using MackySoft.Ucli.Contracts;
using System.Collections;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ResolveOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenArgsContainMultipleSelectors_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    assetPath = "Assets/sample.asset",
                    assetGuid = "11111111111111111111111111111111",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenGlobalObjectIdIsMalformed_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    globalObjectId = "invalid-global-object-id",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [Test]
        [Category("Size.Small")]
        public void ResolvedReference_WhenGlobalObjectIdIsWhitespace_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() => _ = new ResolvedReference(" "));
        }

        [Test]
        [Category("Size.Small")]
        public void ResolvedReference_WhenGlobalObjectIdHasOuterWhitespace_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() => _ = new ResolvedReference(" GlobalObjectId_V1-2-3-4-5-6-7"));
        }

        [Test]
        [Category("Size.Small")]
        public void ResolvedReference_WhenGlobalObjectIdIsMalformed_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() => _ = new ResolvedReference("invalid-global-object-id"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainGlobalObjectId_StoresResolvedReferenceToAliasStore () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out _);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    globalObjectId = expectedGlobalObjectId,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertResolvedGlobalObjectId(result, expectedGlobalObjectId);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainAssetGuid_ResolvesMainAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out var assetPath);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    assetGuid,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainAssetPath_ResolvesMainAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out var assetPath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    assetPath,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainProjectAssetPath_ResolvesMainAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            var projectAssetPath = "ProjectSettings/TagManager.asset";
            var projectAsset = AssetDatabase.LoadMainAssetAtPath(projectAssetPath);
            Assert.That(projectAsset, Is.Not.Null);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(projectAsset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    projectAssetPath,
                });
            using var context = new OperationExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainSceneHierarchyPath_ResolvesGameObjectInLoadedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var enemies = new GameObject("Enemies");
            enemies.transform.SetParent(root.transform, worldPositionStays: false);
            var spawner = new GameObject("Spawner");
            spawner.transform.SetParent(enemies.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(spawner).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Enemies/Spawner",
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSceneIsNotLoaded_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Child",
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainSceneComponentSelector_ResolvesComponentInLoadedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var targetComponent = root.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(targetComponent).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSceneTargetWasDeletedInRequestLocalState_GlobalObjectIdResolveFails () => UniTask.ToCoroutine(async () =>
        {
            var deleteOperation = new GoDeleteOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var deletedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(child).ToString();
            var context = scope.CreateExecutionContext();
            var deleteRequest = new NormalizedOperation(
                Id: "op-delete",
                Op: UcliPrimitiveOperationNames.GoDelete,
                Args: JsonSerializer.SerializeToElement(new
                {
                    target = new
                    {
                        globalObjectId = deletedGlobalObjectId,
                    },
                }),
                As: null,
                Expect: null);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    globalObjectId = deletedGlobalObjectId,
                });

            var deleteResult = await deleteOperation.PlanAsync(deleteRequest, context, CancellationToken.None);
            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(deleteResult.IsSuccess, Is.True, deleteResult.Failure?.Message);
            Assert.That(deleteResult.Applied, Is.False);
            Assert.That(deleteResult.Changed, Is.True);
            Assert.That(deleteResult.Touched.Count, Is.EqualTo(1));
            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTemporarySceneExists_ResolvesStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(child).ToString();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryGetOrOpenTemporaryScene(scenePath, out var previewScene, out var previewErrorMessage), Is.True, previewErrorMessage);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Child",
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTemporaryPrefabRootExists_ResolvesStableReference () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                Id: "op-open",
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(temporaryRoot!, out var temporaryRootReference), Is.True);
            Assert.That(context.TryResolveTemporaryPrefabStableReference(prefabPath, temporaryRoot!, out var temporaryRootStableReference), Is.True);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = temporaryRoot.name,
                });
            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(openResult.IsSuccess, Is.True);
            Assert.That(resolveResult.IsSuccess, Is.True, resolveResult.Failure?.Message);
            Assert.That(resolveResult.Applied, Is.False);
            Assert.That(resolveResult.Changed, Is.False);
            Assert.That(resolveResult.Failure, Is.Null);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(temporaryRootStableReference));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenArgsContainPrefabComponentSelector_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot");
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(editableRoot, Is.Not.Null);
            _ = editableRoot.AddComponent<CompOperationTestComponent>();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);

            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var targetComponent = prefabAssetRoot!.GetComponent<CompOperationTestComponent>();
            Assert.That(targetComponent, Is.Not.Null);
            AssetDatabase.SaveAssets();
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                Id: "op-open",
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });

            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(openResult.IsSuccess, Is.True);
            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTrackedPreviewPrefabTargetHasNoStableGlobalObjectId_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(persistedChild!.gameObject, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            var openRequest = new NormalizedOperation(
                Id: "op-open",
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);

            var previewExistingChild = temporaryRoot!.transform.Find("Child");
            Assert.That(previewExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(previewExistingChild!.gameObject);

            var previewChild = new GameObject("Child");
            previewChild.transform.SetParent(temporaryRoot.transform, worldPositionStays: false);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild, out _), Is.False);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Child",
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyOpenedPrefabStageIsMirrored_DoesNotInventStableReference () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = prefabStage!.prefabContentsRoot.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);
            stageChild!.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var expectedTarget = prefabAssetRoot!.transform.Find("Child");
            Assert.That(expectedTarget, Is.Not.Null);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(expectedTarget!.gameObject).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChild = temporaryRoot!.transform.Find("Renamed");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild!.gameObject, out _), Is.False);
            var hierarchyPath = $"{temporaryRoot.name}/Renamed";

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath,
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyOpenedPrefabStageInsertsSiblingBeforeExistingChild_DoesNotInventStableReference () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "ChildA", "ChildB");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);
            Assert.That(stageRoot!.transform.Find("ChildA"), Is.Not.Null);

            var insertedChild = new GameObject("Inserted");
            insertedChild.transform.SetParent(stageRoot.transform, worldPositionStays: false);
            insertedChild.transform.SetSiblingIndex(0);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var expectedTarget = prefabAssetRoot!.transform.Find("ChildA");
            Assert.That(expectedTarget, Is.Not.Null);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(expectedTarget!.gameObject).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChildA = temporaryRoot!.transform.Find("ChildA");
            Assert.That(previewChildA, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChildA!.gameObject, out _), Is.False);
            var hierarchyPath = $"{temporaryRoot.name}/ChildA";

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath,
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenPrefabTargetWasDeletedInRequestLocalState_GlobalObjectIdResolveFails () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var deleteOperation = new GoDeleteOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                Id: "op-open",
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True, openResult.Failure?.Message);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChild = temporaryRoot!.transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild!.gameObject, out var previewChildReference), Is.True);
            var deleteRequest = new NormalizedOperation(
                Id: "op-delete",
                Op: UcliPrimitiveOperationNames.GoDelete,
                Args: JsonSerializer.SerializeToElement(new
                {
                    target = new
                    {
                        globalObjectId = previewChildReference!.GlobalObjectId,
                    },
                }),
                As: null,
                Expect: null);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    globalObjectId = previewChildReference!.GlobalObjectId,
                });
            var deleteResult = await deleteOperation.PlanAsync(deleteRequest, context, CancellationToken.None);
            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(deleteResult.IsSuccess, Is.True, deleteResult.Failure?.Message);
            Assert.That(deleteResult.Applied, Is.False);
            Assert.That(deleteResult.Changed, Is.True);
            Assert.That(deleteResult.Touched.Count, Is.EqualTo(1));
            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenOpenedPrefabStageAlreadyDeletedPersistedChild_GlobalObjectIdResolveFails () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            var deletedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(persistedChild!.gameObject).ToString();

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = prefabStage!.prefabContentsRoot.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(stageChild!.gameObject);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    globalObjectId = deletedGlobalObjectId,
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenOpenedPrefabStageTargetHasNoStableGlobalObjectId_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(persistedChild!.gameObject, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            var openRequest = new NormalizedOperation(
                Id: "op-open",
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null);
            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);
            Assert.That(openResult.Applied, Is.True);
            Assert.That(openResult.Changed, Is.False);
            Assert.That(openResult.Failure, Is.Null);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);

            var stageExistingChild = stageRoot!.transform.Find("Child");
            Assert.That(stageExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(stageExistingChild!.gameObject);

            var stageChild = new GameObject("Child");
            stageChild.transform.SetParent(stageRoot.transform, worldPositionStays: false);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(stageChild, out _), Is.False);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Child",
                });

            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenOpenedPrefabStageTargetHasNoExplicitStableMapping_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(persistedChild!.gameObject, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            var openRequest = new NormalizedOperation(
                Id: "op-open",
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null);
            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = prefabStage!.prefabContentsRoot.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);
            stageChild!.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(stageChild.gameObject, out _), Is.False);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Renamed",
                });

            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyOpenedPrefabStageHasAmbiguousSiblingNames_DoesNotGuessStableSourceMapping () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child", "Child");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);

            var insertedChild = new GameObject("Inserted");
            insertedChild.transform.SetParent(stageRoot.transform, worldPositionStays: false);
            insertedChild.transform.SetSiblingIndex(0);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);

            var previewFirstChild = temporaryRoot!.transform.GetChild(1).gameObject;
            Assert.That(previewFirstChild.name, Is.EqualTo("Child"));
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = $"{temporaryRoot.name}/Child",
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenOpenedPrefabStageTargetIsMissing_DoesNotFallbackToPersistedPrefabAsset () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                Id: "op-open",
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null);
            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);

            var stageExistingChild = stageRoot!.transform.Find("Child");
            Assert.That(stageExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(stageExistingChild!.gameObject);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Child",
                });

            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTrackedPreviewSceneTargetHasNoStableGlobalObjectId_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var context = scope.CreateExecutionContext();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var persistedChild = new GameObject("Child");
            persistedChild.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(persistedChild, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            Assert.That(context.TryGetOrOpenTemporaryScene(scenePath, out var previewScene, out var previewErrorMessage), Is.True, previewErrorMessage);
            var previewRoot = FindRootGameObject(previewScene, "Root");
            var previewExistingChild = previewRoot.transform.Find("Child");
            Assert.That(previewExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(previewExistingChild!.gameObject);

            var previewChild = new GameObject("Child");
            previewChild.transform.SetParent(previewRoot.transform, worldPositionStays: false);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Child",
                },
                alias: "resolved");

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyLoadedSceneIsMirrored_FallsBackToLiveObjectStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);

            child.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(child).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Renamed");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewChild!.gameObject, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Renamed",
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyLoadedSceneComponentIsMirrored_FallsBackToLiveComponentStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var component = child.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);

            child.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Renamed");
            Assert.That(previewChild, Is.Not.Null);
            var previewComponent = previewChild!.GetComponent<CompOperationTestComponent>();
            Assert.That(previewComponent, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewComponent, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Renamed",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyLoadedSceneComponentWasRecreated_DoesNotGuessStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var persistedComponent = child.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);

            UnityEngine.Object.DestroyImmediate(persistedComponent);
            var recreatedComponent = child.AddComponent<CompOperationTestComponent>();
            Assert.That(recreatedComponent, Is.Not.Null);
            EditorSceneManager.MarkSceneDirty(scene);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);
            var previewComponent = previewChild!.GetComponent<CompOperationTestComponent>();
            Assert.That(previewComponent, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateResolvedReference(previewComponent, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Child",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            if (UnityObjectReferenceResolver.TryCreateResolvedReference(recreatedComponent, out var recreatedReference))
            {
                AssertSuccess(result, applied: false, changed: false);
                Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
                Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(recreatedReference!.GlobalObjectId));
                return;
            }

            AssertInvalidArgument(result, "op-1");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenHierarchyPathResolvesNoObject_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Missing",
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenHierarchyPathResolvesMultipleObjects_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var duplicateA = new GameObject("Dup");
            duplicateA.transform.SetParent(root.transform, worldPositionStays: false);
            var duplicateB = new GameObject("Dup");
            duplicateB.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Dup",
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenResolutionSucceeds_ReturnsAppliedTrueAndChangedFalse () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out _);
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString(),
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
        });

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

        private static NormalizedOperation CreateOperation (
            string opId,
            object args,
            string? alias = null)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: UcliPrimitiveOperationNames.Resolve,
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
            bool changed)
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Touched.Count, Is.EqualTo(0));
            Assert.That(result.Failure, Is.Null);
        }

        private static void AssertResolvedGlobalObjectId (
            OperationPhaseStepResult result,
            string expectedGlobalObjectId)
        {
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Result!.Value.GetProperty("globalObjectId").GetString(), Is.EqualTo(expectedGlobalObjectId));
        }

        private sealed class ResolveTestAsset : ScriptableObject
        {
        }
    }
}
