using System;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ResolveOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public void Validate_WhenArgsContainMultipleSelectors_ReturnsInvalidArgument ()
        {
            var operation = new ResolveOperation();
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    assetPath = "Assets/sample.asset",
                    assetGuid = "11111111111111111111111111111111",
                });

            var result = operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

            AssertInvalidArgument(result, "op-1");
        }

        [Test]
        [Category("Size.Small")]
        public void Validate_WhenGlobalObjectIdIsMalformed_ReturnsInvalidArgument ()
        {
            var operation = new ResolveOperation();
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    globalObjectId = "invalid-global-object-id",
                });

            var result = operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

            AssertInvalidArgument(result, "op-1");
        }

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

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenArgsContainGlobalObjectId_StoresResolvedReferenceToAliasStore ()
        {
            var operation = new ResolveOperation();
            var assetPath = CreateTemporaryAssetPath();
            var asset = ScriptableObject.CreateInstance<ResolveTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
                var requestOperation = CreateOperation(
                    opId: "op-1",
                    alias: "resolved",
                    args: new
                    {
                        globalObjectId = expectedGlobalObjectId,
                    });
                var context = new OperationExecutionContext();

                var result = operation.Plan(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: false, changed: false);
                Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
                Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
            }
            finally
            {
                if (AssetDatabase.Contains(asset))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenArgsContainAssetGuid_ResolvesMainAsset ()
        {
            var operation = new ResolveOperation();
            var assetPath = $"Assets/ResolveOperationTests_{Guid.NewGuid():N}.asset";
            var asset = ScriptableObject.CreateInstance<ResolveTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
                var requestOperation = CreateOperation(
                    opId: "op-1",
                    alias: "resolved",
                    args: new
                    {
                        assetGuid,
                    });
                var context = new OperationExecutionContext();

                var result = operation.Plan(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: false, changed: false);
                Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
                Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
            }
            finally
            {
                if (AssetDatabase.Contains(asset))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenArgsContainAssetPath_ResolvesMainAsset ()
        {
            var operation = new ResolveOperation();
            var assetPath = $"Assets/ResolveOperationTests_{Guid.NewGuid():N}.asset";
            var asset = ScriptableObject.CreateInstance<ResolveTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
                var requestOperation = CreateOperation(
                    opId: "op-1",
                    alias: "resolved",
                    args: new
                    {
                        assetPath,
                    });
                var context = new OperationExecutionContext();

                var result = operation.Plan(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: false, changed: false);
                Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
                Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
            }
            finally
            {
                if (AssetDatabase.Contains(asset))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenArgsContainSceneHierarchyPath_ResolvesGameObjectInLoadedScene ()
        {
            var operation = new ResolveOperation();
            var scenePath = CreateTemporaryScenePath();
            var fallbackScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
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
                var context = new OperationExecutionContext();

                var result = operation.Plan(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: false, changed: false);
                Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
                Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(expectedGlobalObjectId));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
                if (fallbackScene.IsValid())
                {
                    EditorSceneManager.CloseScene(fallbackScene, removeScene: true);
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenSceneIsNotLoaded_ReturnsInvalidArgument ()
        {
            var operation = new ResolveOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
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

                var result = operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

                AssertInvalidArgument(result, "op-1");
            }
            finally
            {
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenHierarchyPathResolvesNoObject_ReturnsInvalidArgument ()
        {
            var operation = new ResolveOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
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

                var result = operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

                AssertInvalidArgument(result, "op-1");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenHierarchyPathResolvesMultipleObjects_ReturnsInvalidArgument ()
        {
            var operation = new ResolveOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
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

                var result = operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

                AssertInvalidArgument(result, "op-1");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Call_WhenResolutionSucceeds_ReturnsAppliedTrueAndChangedFalse ()
        {
            var operation = new ResolveOperation();
            var assetPath = CreateTemporaryAssetPath();
            var asset = ScriptableObject.CreateInstance<ResolveTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
                var requestOperation = CreateOperation(
                    opId: "op-1",
                    alias: "resolved",
                    args: new
                    {
                        globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString(),
                    });
                var context = new OperationExecutionContext();

                var result = operation.Call(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: true, changed: false);
                Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
            }
            finally
            {
                if (AssetDatabase.Contains(asset))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static string CreateTemporaryScenePath ()
        {
            return $"Assets/ResolveOperationTests_{Guid.NewGuid():N}.unity";
        }

        private static string CreateTemporaryAssetPath ()
        {
            return $"Assets/ResolveOperationTests_{Guid.NewGuid():N}.asset";
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            object args,
            string? alias = null)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: "ucli.resolve",
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
            Assert.That(result.Touched.Count, Is.EqualTo(0));
            Assert.That(result.Failure, Is.Null);
        }

        private sealed class ResolveTestAsset : ScriptableObject
        {
        }
    }
}
