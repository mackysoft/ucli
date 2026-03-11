using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PrefabOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task Create_Call_WhenSceneGameObjectIsValid_CreatesPrefabAndConnectsSourceObject ()
        {
            var operation = new PrefabCreateOperation();
            var scenePath = CreateTemporaryScenePath();
            var prefabPath = CreateTemporaryPrefabPath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                var requestOperation = CreateOperation(
                    opId: "op-prefab-create",
                    opName: "ucli.prefab.create",
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
            }
            finally
            {
                ClosePrefabStageIfOpen();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Create_Validate_WhenTargetIsMissing_ReturnsInvalidArgument ()
        {
            var operation = new PrefabCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-prefab-create",
                opName: "ucli.prefab.create",
                args: new
                {
                    path = "Assets/MissingTarget.prefab",
                });

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-create");
        }

        [Test]
        [Category("Size.Small")]
        public async Task Open_Plan_WhenAliasIsSpecified_StoresTemporaryPrefabRootAlias ()
        {
            var operation = new PrefabOpenOperation();
            var prefabPath = CreateTemporaryPrefabPath();
            try
            {
                CreatePrefabAsset(prefabPath, "PrefabRoot");
                var context = new OperationExecutionContext();
                var requestOperation = CreateOperation(
                    opId: "op-prefab-open",
                    opName: "ucli.prefab.open",
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
            }
            finally
            {
                ClosePrefabStageIfOpen();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Open_Plan_WhenFollowedByGoCreateAndCompEnsureAndSet_AllowsTemporaryAliasChain ()
        {
            var openOperation = new PrefabOpenOperation();
            var goCreateOperation = new GoCreateOperation();
            var compEnsureOperation = new CompEnsureOperation();
            var compSetOperation = new CompSetOperation();
            var prefabPath = CreateTemporaryPrefabPath();
            try
            {
                CreatePrefabAsset(prefabPath, "PrefabRoot");
                var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
                var context = new OperationExecutionContext();
                var openRequest = CreateOperation(
                    opId: "op-prefab-open",
                    opName: "ucli.prefab.open",
                    args: new
                    {
                        path = prefabPath,
                    },
                    alias: "root");
                var createRequest = CreateOperation(
                    opId: "op-go-create",
                    opName: "ucli.go.create",
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
                    opName: "ucli.comp.ensure",
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
                    opName: "ucli.comp.set",
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
            }
            finally
            {
                ClosePrefabStageIfOpen();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Open_Call_ThenCompEnsureSet_ThenSave_Call_PersistsComponentChanges ()
        {
            var openOperation = new PrefabOpenOperation();
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var saveOperation = new PrefabSaveOperation();
            var prefabPath = CreateTemporaryPrefabPath();
            GameObject? loadedPrefabContentsRoot = null;
            try
            {
                CreatePrefabAsset(prefabPath, "PrefabRoot");
                var context = new OperationExecutionContext();
                var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
                var openRequest = CreateOperation(
                    opId: "op-prefab-open",
                    opName: "ucli.prefab.open",
                    args: new
                    {
                        path = prefabPath,
                    },
                    alias: "root");
                var ensureRequest = CreateOperation(
                    opId: "op-comp-ensure",
                    opName: "ucli.comp.ensure",
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
                    opName: "ucli.comp.set",
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
                    opName: "ucli.prefab.save",
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

                ClosePrefabStageIfOpen();
                loadedPrefabContentsRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                var component = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
                Assert.That(component, Is.Not.Null);
                Assert.That(component!.IntegerValue, Is.EqualTo(42));
            }
            finally
            {
                if (loadedPrefabContentsRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(loadedPrefabContentsRoot);
                }

                ClosePrefabStageIfOpen();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Open_Call_ThenGoCreateAndCompEnsureSet_ThenSave_Call_PersistsChildChanges ()
        {
            var openOperation = new PrefabOpenOperation();
            var goCreateOperation = new GoCreateOperation();
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var saveOperation = new PrefabSaveOperation();
            var prefabPath = CreateTemporaryPrefabPath();
            GameObject? loadedPrefabContentsRoot = null;
            try
            {
                CreatePrefabAsset(prefabPath, "PrefabRoot");
                var context = new OperationExecutionContext();
                var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
                var openRequest = CreateOperation(
                    opId: "op-prefab-open",
                    opName: "ucli.prefab.open",
                    args: new
                    {
                        path = prefabPath,
                    },
                    alias: "root");
                var createRequest = CreateOperation(
                    opId: "op-go-create",
                    opName: "ucli.go.create",
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
                    opName: "ucli.comp.ensure",
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
                    opName: "ucli.comp.set",
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
                    opName: "ucli.prefab.save",
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

                ClosePrefabStageIfOpen();
                loadedPrefabContentsRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                var child = loadedPrefabContentsRoot.transform.Find("Child");
                Assert.That(child, Is.Not.Null);
                var component = child!.GetComponent<CompOperationTestComponent>();
                Assert.That(component, Is.Not.Null);
                Assert.That(component!.IntegerValue, Is.EqualTo(7));
            }
            finally
            {
                if (loadedPrefabContentsRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(loadedPrefabContentsRoot);
                }

                ClosePrefabStageIfOpen();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        private static void CreatePrefabAsset (
            string prefabPath,
            string rootName)
        {
            var temporaryRoot = new GameObject(rootName);
            try
            {
                Assert.That(PrefabUtility.SaveAsPrefabAsset(temporaryRoot, prefabPath), Is.Not.Null);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static void ClosePrefabStageIfOpen ()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static string CreateTemporaryScenePath ()
        {
            return $"Assets/PrefabOperationTests_{Guid.NewGuid():N}.unity";
        }

        private static string CreateTemporaryPrefabPath ()
        {
            return $"Assets/PrefabOperationTests_{Guid.NewGuid():N}.prefab";
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
