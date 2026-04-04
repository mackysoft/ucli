using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ProjectPhaseOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Refresh_Validate_WhenArgsContainUnknownProperty_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectRefreshPhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: UcliPrimitiveOperationNames.ProjectRefresh,
                args: new
                {
                    unexpected = true,
                });

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-refresh");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Refresh_Plan_WhenArgsAreEmpty_ReturnsNoTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectRefreshPhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: UcliPrimitiveOperationNames.ProjectRefresh,
                args: new { });

            var result = await operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(result.Touched, Is.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Refresh_Call_WhenExternalAssetIsCreated_ImportsAssetAndReturnsTouchedAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectRefreshPhaseOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(ProjectPhaseOperationTests), ".txt");
            var absoluteAssetPath = ToAbsolutePath(assetPath);
            var assetDirectoryPath = Path.GetDirectoryName(absoluteAssetPath);
            if (!string.IsNullOrWhiteSpace(assetDirectoryPath))
            {
                Directory.CreateDirectory(assetDirectoryPath);
            }

            File.WriteAllText(absoluteAssetPath, "refresh-test");
            var requestOperation = CreateOperation(
                opId: "op-refresh",
                opName: UcliPrimitiveOperationNames.ProjectRefresh,
                args: new { });

            var result = await operation.Call(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath), Is.Not.Null);
            Assert.That(result.Touched.Any(touched => touched.Path == assetPath && touched.Kind == OperationTouchKind.Asset), Is.True);
        });

        [Test]
        [Category("Size.Small")]
        public void SyncDirtyStateChanges_WhenSceneTransitionsToDirty_MarksRequestAttributedChangeAndTouchesScene ()
        {
            var scenePath = "Assets/ProjectPhaseOperationTests_Scene.unity";
            var executionContext = new OperationExecutionContext();
            var touched = new List<OperationTouch>();

            ProjectOperationUtilities.SyncDirtyStateChanges(
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [scenePath] = false,
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [scenePath] = true,
                },
                OperationTouchKind.Scene,
                touched,
                executionContext);

            var resource = new OperationResource(OperationTouchKind.Scene, scenePath);
            Assert.That(executionContext.HasRequestAttributedChange(resource), Is.True);
            Assert.That(touched, Has.Count.EqualTo(1));
            Assert.That(touched[0].Kind, Is.EqualTo(OperationTouchKind.Scene));
            Assert.That(touched[0].Path, Is.EqualTo(scenePath));
        }

        [Test]
        [Category("Size.Small")]
        public void SyncDirtyStateChanges_WhenPrefabTransitionsToClean_ClearsRequestAttributedChangeAndTouchesPrefab ()
        {
            var prefabPath = "Assets/ProjectPhaseOperationTests_Prefab.prefab";
            var executionContext = new OperationExecutionContext();
            var resource = new OperationResource(OperationTouchKind.Prefab, prefabPath);
            executionContext.MarkRequestAttributedChange(resource);
            var touched = new List<OperationTouch>();

            ProjectOperationUtilities.SyncDirtyStateChanges(
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [prefabPath] = true,
                },
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [prefabPath] = false,
                },
                OperationTouchKind.Prefab,
                touched,
                executionContext);

            Assert.That(executionContext.HasRequestAttributedChange(resource), Is.False);
            Assert.That(touched, Has.Count.EqualTo(1));
            Assert.That(touched[0].Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(touched[0].Path, Is.EqualTo(prefabPath));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenArgsAreEmpty_ReturnsNoTouchedResources () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(result.Touched, Is.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenScriptableObjectAssetIsDirty_SavesAssetAndReturnsTouchedAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<IndexCatalogTestAsset>(nameof(ProjectPhaseOperationTests), out var assetPath);
            AssetDatabase.SaveAssets();

            var serializedObject = new SerializedObject(asset);
            serializedObject.FindProperty("speed").floatValue = 42.0f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            Assert.That(EditorUtility.IsDirty(asset), Is.True);

            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.Call(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(EditorUtility.IsDirty(asset), Is.False);
            Assert.That(result.Touched.Any(touched => touched.Path == assetPath && touched.Kind == OperationTouchKind.Asset), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOnlySceneIsDirty_DoesNotSaveScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ProjectSavePhaseOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ProjectPhaseOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            _ = new GameObject("DirtySceneObject");
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(scene.isDirty, Is.True);
            var requestOperation = CreateOperation(
                opId: "op-save",
                opName: UcliPrimitiveOperationNames.ProjectSave,
                args: new { });

            var result = await operation.Call(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: false);
            Assert.That(scene.isDirty, Is.True);
            Assert.That(result.Touched.Any(touched => touched.Kind == OperationTouchKind.Scene), Is.False);
        });

        private static string ToAbsolutePath (string assetPath)
        {
            return Path.Combine(
                UnityProjectPathResolver.ResolveProjectRootPath(),
                PathStringNormalizer.ToPlatformSeparated(assetPath));
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
