using System;
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
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-create",
                    opName: "ucli.go.create",
                    args: new
                    {
                        name = "CreatedRoot",
                        scene = scenePath,
                    },
                    alias: "created");
                var context = new OperationExecutionContext();

                var result = await operation.Call(requestOperation, context, CancellationToken.None);

                AssertSuccess(result, applied: true, changed: true);
                var loadedScene = SceneManager.GetSceneByPath(scenePath);
                Assert.That(loadedScene.IsValid(), Is.True);
                Assert.That(loadedScene.isLoaded, Is.True);
                Assert.That(loadedScene.GetRootGameObjects().Any(static gameObject => gameObject.name == "CreatedRoot"), Is.True);
                Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenParentUsesAlias_CreatesChildUnderParent () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var parent = new GameObject("Parent");
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("parent", UnityObjectReferenceResolver.CreateResolvedReference(parent));
                var requestOperation = CreateOperation(
                    opId: "op-create",
                    opName: "ucli.go.create",
                    args: new
                    {
                        name = "CreatedChild",
                        parent = new
                        {
                            @var = "parent",
                        },
                    },
                    alias: "created");

                var result = await operation.Call(requestOperation, context, CancellationToken.None);

                AssertSuccess(result, applied: true, changed: true);
                Assert.That(parent.transform.childCount, Is.EqualTo(1));
                Assert.That(parent.transform.GetChild(0).name, Is.EqualTo("CreatedChild"));
                Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenParentUsesSelector_CreatesChildUnderParent () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var parent = new GameObject("Parent");
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-create",
                    opName: "ucli.go.create",
                    args: new
                    {
                        name = "CreatedChild",
                        parent = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Parent",
                        },
                    });

                var result = await operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertSuccess(result, applied: true, changed: true);
                Assert.That(parent.transform.childCount, Is.EqualTo(1));
                Assert.That(parent.transform.GetChild(0).name, Is.EqualTo("CreatedChild"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenAliasIsSpecified_StoresTemporaryAlias () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-create",
                    opName: "ucli.go.create",
                    args: new
                    {
                        name = "CreatedRoot",
                        scene = scenePath,
                    },
                    alias: "created");
                var context = new OperationExecutionContext();

                var result = await operation.Plan(requestOperation, context, CancellationToken.None);

                AssertSuccess(result, applied: false, changed: true);
                Assert.That(context.TryGetTemporaryAliasState("created", out var temporaryAliasState), Is.True);
                Assert.That(context.AliasStore.TryGet("created", out _), Is.False);
                Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(scenePath));
                Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<GameObject>());
                Assert.That(((GameObject)temporaryAliasState.UnityObject!).name, Is.EqualTo("CreatedRoot"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenSceneAndParentAreBothSpecified_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: "ucli.go.create",
                args: new
                {
                    name = "CreatedRoot",
                    scene = "Assets/Scenes/Main.unity",
                    parent = new
                    {
                        @var = "parent",
                    },
                });

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenSceneAndParentAreBothMissing_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: "ucli.go.create",
                args: new
                {
                    name = "CreatedRoot",
                });

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenParentResolvesAsset_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoCreateOperation();
            var assetPath = CreateTemporaryAssetPath();
            var asset = ScriptableObject.CreateInstance<IndexCatalogTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var requestOperation = CreateOperation(
                    opId: "op-create",
                    opName: "ucli.go.create",
                    args: new
                    {
                        name = "CreatedChild",
                        parent = new
                        {
                            assetPath,
                        },
                    });

                var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertInvalidArgument(result, "op-create");
            }
            finally
            {
                if (AssetDatabase.Contains(asset))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenTargetUsesAlias_ReturnsSuccess () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                root.AddComponent<BoxCollider>();
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(root));
                var requestOperation = CreateOperation(
                    opId: "op-describe",
                    opName: "ucli.go.describe",
                    args: new
                    {
                        target = new
                        {
                            @var = "target",
                        },
                        depth = 1,
                    });

                var result = await operation.Plan(requestOperation, context, CancellationToken.None);

                AssertSuccess(result, applied: false, changed: false);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Plan_WhenTargetUsesSelector_ReturnsSuccess () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var child = new GameObject("Child");
                child.transform.SetParent(root.transform, worldPositionStays: false);
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-describe",
                    opName: "ucli.go.describe",
                    args: new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root/Child",
                        },
                    });

                var result = await operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertSuccess(result, applied: false, changed: false);
                Assert.That(result.Result.HasValue, Is.True);
                Assert.That(result.Result!.Value.GetProperty("name").GetString(), Is.EqualTo("Child"));
                Assert.That(result.Result.Value.GetProperty("children").GetArrayLength(), Is.EqualTo(0));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Validate_WhenDepthIsNegative_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            var requestOperation = CreateOperation(
                opId: "op-describe",
                opName: "ucli.go.describe",
                args: new
                {
                    target = new
                    {
                        @var = "target",
                    },
                    depth = -1,
                });

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-describe");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Describe_Validate_WhenTargetResolvesAsset_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new GoDescribeOperation();
            var assetPath = CreateTemporaryAssetPath();
            var asset = ScriptableObject.CreateInstance<IndexCatalogTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var requestOperation = CreateOperation(
                    opId: "op-describe",
                    opName: "ucli.go.describe",
                    args: new
                    {
                        target = new
                        {
                            assetPath,
                        },
                    });

                var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertInvalidArgument(result, "op-describe");
            }
            finally
            {
                if (AssetDatabase.Contains(asset))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }
        });

        [Test]
        [Category("Size.Small")]
        public void DescriptionBuilder_Build_WhenDepthIsOne_CapturesComponentsAndChildren ()
        {
            var scenePath = CreateTemporaryScenePath();
            try
            {
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
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        private static string CreateTemporaryScenePath ()
        {
            return $"Assets/GoOperationTests_{Guid.NewGuid():N}.unity";
        }

        private static string CreateTemporaryAssetPath ()
        {
            return $"Assets/GoOperationTests_{Guid.NewGuid():N}.asset";
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
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Scene));
            Assert.That(result.Failure, Is.Null);
        }
    }
}