using System;
using MackySoft.Ucli.Contracts;
using System.Collections;
using System.IO;
using System.Linq;
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
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PrefabOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenSceneGameObjectIsValid_CreatesPrefabAndConnectsSourceObject () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabCreateOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var prefabPath = scope.CreatePrefabPath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
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
                },
                alias: "created");

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(result.Persisted, Is.True);
            AssertTouchSet(
                result,
                (OperationTouchKind.Scene, scenePath),
                (OperationTouchKind.Prefab, prefabPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(root), Is.True);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(root), Is.Not.Null);
            Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(UnityObjectReferenceResolver.CreateResolvedReference(root).GlobalObjectId));
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.AssetSearch, null),
                (OperationReadInvalidationSurface.GuidPath, null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenTargetIsMissing_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-prefab-create",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    path = "Assets/MissingTarget.prefab",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_WhenDirtyLoadedSceneExists_ReturnsInvalidArgumentBeforeOpening () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("DirtyRoot");
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty loaded scene blocks opening prefab"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenDirtyLoadedSceneExists_ReturnsInvalidArgumentBeforePlanning () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("DirtyRoot");
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty loaded scene blocks opening prefab"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenAnotherDirtyPrefabStageExists_ReturnsInvalidArgumentBeforePlanning () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var dirtyPrefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "DirtyPrefabRoot");
            var targetPrefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "TargetPrefabRoot");
            var dirtyPrefabStage = PrefabStageUtility.OpenPrefab(dirtyPrefabPath);
            var child = new GameObject("DirtyChild");
            child.transform.SetParent(dirtyPrefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(dirtyPrefabStage.scene);
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = targetPrefabPath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty prefab stage blocks opening prefab"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenAliasIsSpecified_StoresTemporaryPrefabRootAlias () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertTouchSet(result, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryAliasState("root", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<GameObject>());
            Assert.That(
                ((GameObject)temporaryAliasState.UnityObject!).name,
                Is.EqualTo(Path.GetFileNameWithoutExtension(prefabPath)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenRawPrefabPathIsValid_DoesNotTrackTemporaryPrefabRoot () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root");

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertTouchSet(result, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out _), Is.False);
            Assert.That(context.TryGetTemporaryAliasState("root", out _), Is.False);
            Assert.That(PrefabStageUtility.GetCurrentPrefabStage(), Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenFollowedByGoCreateAndCompEnsureAndSet_AllowsTemporaryAliasChain () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var goCreateOperation = new GoCreateOperation();
            var compEnsureOperation = new CompEnsureOperation();
            var compSetOperation = new CompSetOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var createRequest = CreateOperation(
                opId: "op-go-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "Child",
                    parent = new
                    {
                        @var = "root",
                    },
                },
                alias: "child",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        @var = "child",
                    },
                    type = componentTypeId,
                },
                alias: "childComp",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var setRequest = CreateOperation(
                opId: "op-comp-set",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        @var = "childComp",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 11,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            var createResult = await goCreateOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var ensureResult = await compEnsureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);
            var setResult = await compSetOperation.PlanAsync(setRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);
            AssertSuccess(createResult, applied: false, changed: true);
            AssertSuccess(ensureResult, applied: false, changed: true);
            AssertSuccess(setResult, applied: false, changed: true);
            Assert.That(context.TryGetTemporaryAliasState("childComp", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<CompOperationTestComponent>());
            Assert.That(((CompOperationTestComponent)temporaryAliasState.UnityObject!).IntegerValue, Is.EqualTo(11));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenOpenedPrefabStageIsDirty_TracksTemporaryPrefabContentsSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot", "Child");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            prefabStage!.prefabContentsRoot.transform.GetChild(0).name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertTouchSet(result, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var prefabContentsRoot), Is.True);
            Assert.That(prefabContentsRoot, Is.Not.Null);
            Assert.That(prefabContentsRoot, Is.Not.SameAs(prefabStage.prefabContentsRoot));
            Assert.That(prefabContentsRoot!.transform.GetChild(0).name, Is.EqualTo("Renamed"));
            Assert.That(context.TryGetTemporaryAliasState("root", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.SameAs(prefabContentsRoot));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenOpenedPrefabStageIsDirty_RebindsCrossRootObjectReferencesInsideTemporaryPrefabSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            var sourceA = new GameObject("A");
            sourceA.transform.SetParent(editableRoot.transform, worldPositionStays: false);
            var sourceComponent = sourceA.AddComponent<CompOperationTestComponent>();
            var sourceB = new GameObject("B");
            sourceB.transform.SetParent(editableRoot.transform, worldPositionStays: false);
            var serializedObject = new SerializedObject(sourceComponent);
            serializedObject.FindProperty("objectReferenceValue").objectReferenceValue = sourceB;
            serializedObject.FindProperty("componentReferenceValue").objectReferenceValue = sourceB.transform;
            serializedObject.FindProperty("exposedObjectReferenceValue.defaultValue").objectReferenceValue = sourceB;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            var stageRoot = prefabStage!.prefabContentsRoot;
            var stageB = stageRoot.transform.Find("B");
            Assert.That(stageB, Is.Not.Null);
            stageB!.name = "RenamedB";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var prefabContentsRoot), Is.True);
            Assert.That(prefabContentsRoot, Is.Not.Null);
            var previewA = prefabContentsRoot!.transform.Find("A");
            var previewB = prefabContentsRoot.transform.Find("RenamedB");
            Assert.That(previewA, Is.Not.Null);
            Assert.That(previewB, Is.Not.Null);
            var previewComponent = previewA!.GetComponent<CompOperationTestComponent>();
            Assert.That(previewComponent, Is.Not.Null);
            Assert.That(previewComponent!.ObjectReferenceValue, Is.SameAs(previewB!.gameObject));
            Assert.That(previewComponent.ObjectReferenceValue, Is.Not.SameAs(stageB.gameObject));
            Assert.That(previewComponent.ComponentReferenceValue, Is.SameAs(previewB.transform));
            Assert.That(previewComponent.ComponentReferenceValue, Is.Not.SameAs(stageB.transform));
            Assert.That(previewComponent.ExposedObjectReferenceValue, Is.SameAs(previewB.gameObject));
            Assert.That(previewComponent.ExposedObjectReferenceValue, Is.Not.SameAs(stageB.gameObject));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenCompEnsureTargetsPrefabSelector_UsesPrefabOwnerResource () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var ensureOperation = new CompEnsureOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        prefab = prefabPath,
                        hierarchyPath = Path.GetFileNameWithoutExtension(prefabPath),
                    },
                    type = componentTypeId,
                },
                alias: "component",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);
            AssertSuccess(ensureResult, applied: false, changed: true);
            AssertTouchSet(ensureResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryAliasState("component", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<CompOperationTestComponent>());
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_ThenCompEnsureSet_ThenSave_Call_PersistsComponentChanges () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root");
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        @var = "root",
                    },
                    type = componentTypeId,
                },
                alias: "component");
            var setRequest = CreateOperation(
                opId: "op-comp-set",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        @var = "component",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                });
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.CallAsync(ensureRequest, context, CancellationToken.None);
            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: true, changed: false);
            AssertSuccess(ensureResult, applied: true, changed: true);
            AssertSuccess(setResult, applied: true, changed: true);
            AssertSuccess(saveResult, applied: true, changed: true);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            var openedPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(openedPrefabStage, Is.Not.Null);
            Assert.That(openedPrefabStage!.prefabContentsRoot.scene.isDirty, Is.False);

            scope.CloseCurrentPrefabStageIfOpen();
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            var component = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            Assert.That(component!.IntegerValue, Is.EqualTo(42));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_ThenGoCreateAndCompEnsureSet_ThenSave_Call_PersistsChildChanges () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var goCreateOperation = new GoCreateOperation();
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root");
            var createRequest = CreateOperation(
                opId: "op-go-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "Child",
                    parent = new
                    {
                        @var = "root",
                    },
                },
                alias: "child");
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        @var = "child",
                    },
                    type = componentTypeId,
                },
                alias: "childComp");
            var setRequest = CreateOperation(
                opId: "op-comp-set",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        @var = "childComp",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 7,
                        },
                    },
                });
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            var createResult = await goCreateOperation.CallAsync(createRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.CallAsync(ensureRequest, context, CancellationToken.None);
            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: true, changed: false);
            AssertSuccess(createResult, applied: true, changed: true);
            AssertSuccess(ensureResult, applied: true, changed: true);
            AssertSuccess(setResult, applied: true, changed: true);
            AssertSuccess(saveResult, applied: true, changed: true);

            scope.CloseCurrentPrefabStageIfOpen();
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            var child = loadedPrefabContentsRoot.transform.Find("Child");
            Assert.That(child, Is.Not.Null);
            var component = child!.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            Assert.That(component!.IntegerValue, Is.EqualTo(7));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOpenedPrefabStageIsDirtyWithoutRequestChange_SavesOpenedPrefabStage () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            var child = new GameObject("Child");
            child.transform.SetParent(prefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.prefabContentsRoot.scene);
            Assert.That(prefabStage.prefabContentsRoot.scene.isDirty, Is.True);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            Assert.That(saveResult.Persisted, Is.True);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(prefabStage.prefabContentsRoot.scene.isDirty, Is.False);
            AssertReadInvalidations(
                saveResult,
                (OperationReadInvalidationSurface.AssetSearch, null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenOnlyTemporaryPrefabPreviewExistsWithoutPlannedLiveOpen_Succeeds () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.PlanAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: false, changed: false);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOnlyTemporaryPrefabPreviewHasRequestAttributedChange_SavesTemporaryPrefabContents () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            var child = new GameObject("SavedChild");
            child.transform.SetParent(temporaryRoot!.transform, worldPositionStays: false);
            context.MarkRequestAttributedChange(new OperationResource(OperationTouchKind.Prefab, prefabPath));
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            Assert.That(saveResult.Persisted, Is.True);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Prefab, prefabPath)), Is.False);

            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(loadedPrefabContentsRoot.transform.Find("SavedChild"), Is.Not.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOpenedPrefabStageAndTemporaryPreviewExist_SavesOpenedPrefabStage () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            var temporaryChild = new GameObject("TemporaryChild");
            temporaryChild.transform.SetParent(temporaryRoot!.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(temporaryRoot.scene);

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = new GameObject("StageChild");
            stageChild.transform.SetParent(prefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            Assert.That(saveResult.Persisted, Is.True);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(prefabStage.scene.isDirty, Is.False);
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(loadedPrefabContentsRoot.transform.Find("StageChild"), Is.Not.Null);
            Assert.That(loadedPrefabContentsRoot.transform.Find("TemporaryChild"), Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RevertOverrides_Call_WhenPlanPreviewExists_RevertsLivePrefabInstance () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var revertOperation = new PrefabRevertOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            _ = editableRoot.AddComponent<CompOperationTestComponent>();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);

            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAsset, Is.Not.Null);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset!);
            instance.name = "InstanceRoot";
            var component = instance.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            EditorSceneManager.SaveScene(scene, scenePath);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var target = new
            {
                scene = scenePath,
                hierarchyPath = "InstanceRoot",
                componentType = componentTypeId,
            };
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                });
            var revertRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabRevertOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabPath,
                    propertyPaths = new[] { "integerValue" },
                });

            var planSetResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var callSetResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var revertResult = await revertOperation.CallAsync(revertRequest, context, CancellationToken.None);

            AssertSuccess(planSetResult, applied: false, changed: true);
            AssertSuccess(callSetResult, applied: true, changed: true);
            AssertSuccess(revertResult, applied: true, changed: true);
            AssertTouchSet(revertResult, (OperationTouchKind.Scene, scenePath));
            AssertReadInvalidations(
                revertResult,
                (OperationReadInvalidationSurface.SceneTreeLite, scenePath.Replace('\\', '/')));
            Assert.That(component!.IntegerValue, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenPrefabOpenWasPlannedForClosedPrefab_Succeeds () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var openPlanResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            var savePlanResult = await saveOperation.PlanAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(openPlanResult, applied: false, changed: false);
            AssertSuccess(savePlanResult, applied: false, changed: false);
            Assert.That(savePlanResult.Persisted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenPrefabIsNotOpened_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(saveResult, "op-prefab-save");
        });

        private static NormalizedOperation CreateOperation (
            string opId,
            string opName,
            object args,
            string? alias = null,
            NormalizedOperation.SourceStepKind sourceKind = NormalizedOperation.SourceStepKind.Op)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: alias,
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
            bool changed)
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Failure, Is.Null);
        }

        private static void AssertTouchSet (
            OperationPhaseStepResult result,
            params (OperationTouchKind Kind, string Path)[] expectedTouches)
        {
            Assert.That(result.Touched.Count, Is.EqualTo(expectedTouches.Length));
            for (var i = 0; i < expectedTouches.Length; i++)
            {
                var expectedTouch = expectedTouches[i];
                Assert.That(
                    result.Touched.Any(touch =>
                        touch.Kind == expectedTouch.Kind
                        && touch.Path == expectedTouch.Path),
                    Is.True,
                    $"Touched resource was not found. kind={expectedTouch.Kind}, path={expectedTouch.Path}");
            }
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
