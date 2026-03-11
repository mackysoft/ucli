using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class SceneOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task Open_Call_WhenScenePathIsValid_OpensTargetScene ()
        {
            var operation = new SceneOpenOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var createdScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = new GameObject("Root");
                EditorSceneManager.SaveScene(createdScene, scenePath);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var requestOperation = CreateOperation(
                    opId: "op-open",
                    opName: "ucli.scene.open",
                    args: new
                    {
                        path = scenePath,
                    });

                var result = await operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertSuccess(result, applied: true, changed: false);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Save_Call_WhenSceneIsDirty_ReturnsChangedTrueAndSavesScene ()
        {
            var operation = new SceneSaveOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                _ = new GameObject("DirtyObject");
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(scene.isDirty, Is.True);
                var requestOperation = CreateOperation(
                    opId: "op-save",
                    opName: "ucli.scene.save",
                    args: new
                    {
                        path = scenePath,
                    });

                var result = await operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertSuccess(result, applied: true, changed: true);
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Tree_Validate_WhenDepthIsNegative_ReturnsInvalidArgument ()
        {
            var operation = new SceneTreeOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-tree",
                    opName: "ucli.scene.tree",
                    args: new
                    {
                        path = scenePath,
                        depth = -1,
                    });

                var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertInvalidArgument(result, "op-tree");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Tree_Plan_WhenDepthIsNull_AcceptsUnlimitedDepth ()
        {
            var operation = new SceneTreeOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var child = new GameObject("Child");
                child.transform.SetParent(root.transform, worldPositionStays: false);
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-tree",
                    opName: "ucli.scene.tree",
                    args: new
                    {
                        path = scenePath,
                        depth = (int?)null,
                    });

                var result = await operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertSuccess(result, applied: false, changed: false);
                Assert.That(result.Result.HasValue, Is.True);
                Assert.That(result.Result!.Value.GetProperty("path").GetString(), Is.EqualTo(scenePath));
                Assert.That(result.Result.Value.GetProperty("roots").GetArrayLength(), Is.EqualTo(1));
                Assert.That(result.Result.Value.GetProperty("roots")[0].GetProperty("children").GetArrayLength(), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Tree_Plan_WhenDepthIsOmitted_AcceptsUnlimitedDepth ()
        {
            var operation = new SceneTreeOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var child = new GameObject("Child");
                child.transform.SetParent(root.transform, worldPositionStays: false);
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-tree",
                    opName: "ucli.scene.tree",
                    args: new
                    {
                        path = scenePath,
                    });

                var result = await operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertSuccess(result, applied: false, changed: false);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        private static string CreateTemporaryScenePath ()
        {
            return $"Assets/SceneOperationTests_{Guid.NewGuid():N}.unity";
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
