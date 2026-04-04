using System;
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

            var result = await operation.Call(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            AssertTouchSet(
                result,
                (OperationTouchKind.Scene, scenePath),
                (OperationTouchKind.Prefab, prefabPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(root), Is.True);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(root), Is.Not.Null);
            Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
            Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(UnityObjectReferenceResolver.CreateResolvedReference(root).GlobalObjectId));
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

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-create");
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
                alias: "root");

            var result = await operation.Plan(requestOperation, context, CancellationToken.None);

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
                            value = 11,
                        },
                    },
                });

            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);
            var createResult = await goCreateOperation.Plan(createRequest, context, CancellationToken.None);
            var ensureResult = await compEnsureOperation.Plan(ensureRequest, context, CancellationToken.None);
            var setResult = await compSetOperation.Plan(setRequest, context, CancellationToken.None);

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
                alias: "root");

            var result = await operation.Plan(requestOperation, context, CancellationToken.None);

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
                });
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
                alias: "component");

            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.Plan(ensureRequest, context, CancellationToken.None);

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

            var openResult = await openOperation.Call(openRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.Call(ensureRequest, context, CancellationToken.None);
            var setResult = await setOperation.Call(setRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.Call(saveRequest, context, CancellationToken.None);

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

            var openResult = await openOperation.Call(openRequest, context, CancellationToken.None);
            var createResult = await goCreateOperation.Call(createRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.Call(ensureRequest, context, CancellationToken.None);
            var setResult = await setOperation.Call(setRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.Call(saveRequest, context, CancellationToken.None);

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
        public IEnumerator Save_Call_WhenOpenedPrefabStageIsDirtyWithoutRequestChange_SavesStageContents () => UniTask.ToCoroutine(async () =>
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

            var saveResult = await saveOperation.Call(saveRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(prefabStage.prefabContentsRoot.scene.isDirty, Is.False);

            scope.CloseCurrentPrefabStageIfOpen();
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(loadedPrefabContentsRoot.transform.Find("Child"), Is.Not.Null);
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

            var saveResult = await saveOperation.Call(saveRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(saveResult, "op-prefab-save");
        });

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
            Assert.That(result.Failure!.Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
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
    }
}
