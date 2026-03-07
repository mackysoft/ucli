using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ProjectPhaseOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public void Refresh_Validate_WhenArgsContainUnknownProperty_ReturnsInvalidArgument ()
        {
            var operation = new ProjectRefreshPhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: "ucli.project.refresh",
                args: new
                {
                    unexpected = true,
                });

            var result = operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

            AssertInvalidArgument(result, "op-refresh");
        }

        [Test]
        [Category("Size.Small")]
        public void Refresh_Plan_WhenArgsAreEmpty_ReturnsNoTouchedResources ()
        {
            var operation = new ProjectRefreshPhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: "ucli.project.refresh",
                args: new { });

            var result = operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(result.Touched, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void Refresh_Call_WhenExternalAssetIsCreated_ImportsAssetAndReturnsTouchedAsset ()
        {
            var operation = new ProjectRefreshPhaseOperation();
            var assetPath = CreateTemporaryTextAssetPath();
            var absoluteAssetPath = ToAbsolutePath(assetPath);
            var assetDirectoryPath = Path.GetDirectoryName(absoluteAssetPath);
            try
            {
                if (!string.IsNullOrWhiteSpace(assetDirectoryPath))
                {
                    Directory.CreateDirectory(assetDirectoryPath);
                }

                File.WriteAllText(absoluteAssetPath, "refresh-test");
                var requestOperation = CreateOperation(
                    opId: "op-refresh",
                    opName: "ucli.project.refresh",
                    args: new { });

                var result = operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: true, changed: true);
                Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath), Is.Not.Null);
                Assert.That(result.Touched.Any(touched => touched.Path == assetPath && touched.Kind == OperationTouchKind.Asset), Is.True);
            }
            finally
            {
                DeleteAssetAndFiles(assetPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Save_Plan_WhenArgsAreEmpty_ReturnsNoTouchedResources ()
        {
            var operation = new ProjectSavePhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: "ucli.project.save",
                args: new { });

            var result = operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(result.Touched, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void Save_Call_WhenScriptableObjectAssetIsDirty_SavesAssetAndReturnsTouchedAsset ()
        {
            var operation = new ProjectSavePhaseOperation();
            var assetPath = CreateTemporaryAssetPath();
            var asset = ScriptableObject.CreateInstance<IndexCatalogTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();

                var serializedObject = new SerializedObject(asset);
                serializedObject.FindProperty("speed").floatValue = 42.0f;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                Assert.That(EditorUtility.IsDirty(asset), Is.True);

                var requestOperation = CreateOperation(
                    opId: "op-save",
                    opName: "ucli.project.save",
                    args: new { });

                var result = operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: true, changed: true);
                Assert.That(EditorUtility.IsDirty(asset), Is.False);
                Assert.That(result.Touched.Any(touched => touched.Path == assetPath && touched.Kind == OperationTouchKind.Asset), Is.True);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(asset, allowDestroyingAssets: true);
                DeleteAssetAndFiles(assetPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Save_Call_WhenOnlySceneIsDirty_DoesNotSaveScene ()
        {
            var operation = new ProjectSavePhaseOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                _ = new GameObject("DirtySceneObject");
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(scene.isDirty, Is.True);
                var requestOperation = CreateOperation(
                    opId: "op-save",
                    opName: "ucli.project.save",
                    args: new { });

                var result = operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: true, changed: false);
                Assert.That(scene.isDirty, Is.True);
                Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene), Is.False);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        private static string CreateTemporaryTextAssetPath ()
        {
            return $"Assets/ProjectPhaseOperationTests_{Guid.NewGuid():N}.txt";
        }

        private static string CreateTemporaryAssetPath ()
        {
            return $"Assets/ProjectPhaseOperationTests_{Guid.NewGuid():N}.asset";
        }

        private static string CreateTemporaryScenePath ()
        {
            return $"Assets/ProjectPhaseOperationTests_{Guid.NewGuid():N}.unity";
        }

        private static string ToAbsolutePath (string assetPath)
        {
            return Path.Combine(
                UnityProjectPathResolver.ResolveProjectRootPath(),
                PathStringNormalizer.ToPlatformSeparated(assetPath));
        }

        private static void DeleteAssetAndFiles (string assetPath)
        {
            _ = AssetDatabase.DeleteAsset(assetPath);
            var absolutePath = ToAbsolutePath(assetPath);
            var metaPath = absolutePath + ".meta";
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
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
            Assert.That(result.Failure, Is.Null);
        }
    }
}
