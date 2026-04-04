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

            var result = await operation.Call(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            var loadedScene = SceneManager.GetSceneByPath(scenePath);
            Assert.That(loadedScene.IsValid(), Is.True);
            Assert.That(loadedScene.isLoaded, Is.True);
            Assert.That(loadedScene.GetRootGameObjects().Any(static gameObject => gameObject.name == "CreatedRoot"), Is.True);
            Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
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

            var result = await operation.Call(requestOperation, context, CancellationToken.None);

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

            var result = await operation.Call(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(parent.transform.childCount, Is.EqualTo(1));
            Assert.That(parent.transform.GetChild(0).name, Is.EqualTo("CreatedChild"));
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
            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);

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

            var result = await operation.Plan(requestOperation, context, CancellationToken.None);

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
                opName: UcliPrimitiveOperationNames.GoCreate,
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

            var result = await operation.Validate(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

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

            var result = await operation.Plan(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
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

            var result = await operation.Plan(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Result!.Value.GetProperty("name").GetString(), Is.EqualTo("Child"));
            Assert.That(result.Result.Value.GetProperty("children").GetArrayLength(), Is.EqualTo(0));
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

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

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

            var result = await operation.Validate(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

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

            var result = await operation.Plan(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenSceneIsLoadedWithoutPriorOpen_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
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
                });

            var result = await operation.Plan(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenParentIsLiveSceneObjectWithoutPriorOpen_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
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

            var result = await operation.Plan(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
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
            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                });

            var createResult = await createOperation.Call(createRequest, context, CancellationToken.None);

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
            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);

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
            var deleteResult = await deleteOperation.Plan(deleteRequest, context, CancellationToken.None);

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

            var describeResult = await describeOperation.Plan(describeRequest, context, CancellationToken.None);

            AssertInvalidArgument(describeResult, "op-describe");
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
            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);

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

            var deleteResult = await deleteOperation.Call(deleteRequest, context, CancellationToken.None);

            AssertInvalidArgument(deleteResult, "op-delete");
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
            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);

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

            var reparentResult = await reparentOperation.Plan(reparentRequest, context, CancellationToken.None);

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
            var newPathResult = await describeOperation.Plan(newPathDescribe, context, CancellationToken.None);

            AssertSuccess(newPathResult, applied: false, changed: false);
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
            var oldPathResult = await describeOperation.Plan(oldPathDescribe, context, CancellationToken.None);

            AssertInvalidArgument(oldPathResult, "op-describe-old");
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
            var openResult = await openOperation.Plan(openRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);

            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "CreatedRoot",
                    scene = scenePath,
                });

            var createResult = await createOperation.Plan(createRequest, context, CancellationToken.None);

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
            var describeResult = await describeOperation.Plan(describeRequest, context, CancellationToken.None);

            AssertSuccess(describeResult, applied: false, changed: false);
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
