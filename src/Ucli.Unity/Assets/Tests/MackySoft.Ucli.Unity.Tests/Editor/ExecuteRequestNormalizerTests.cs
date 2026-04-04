using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecuteRequestNormalizerTests
    {
        private const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOpRequestIsValid_ReturnsNormalizedRequestAndCanonicalPayload ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            id = "resolve",
                            op = UcliPrimitiveOperationNames.Resolve,
                            args = new
                            {
                                scene = "Assets/Scenes/Main.unity",
                                hierarchyPath = "Root/Enemies/Spawner",
                            },
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);

            var normalizedRequest = result.Request!;
            Assert.That(normalizedRequest.ProtocolVersion, Is.EqualTo(IpcProtocol.CurrentVersion));
            Assert.That(normalizedRequest.RequestId, Is.EqualTo(RequestId));
            Assert.That(normalizedRequest.SourceSteps.Count, Is.EqualTo(1));
            Assert.That(normalizedRequest.SourceSteps[0].Kind, Is.EqualTo(IpcRequestStepKind.Op));
            Assert.That(normalizedRequest.SourceSteps[0].OperationName, Is.EqualTo(UcliPrimitiveOperationNames.Resolve));

            var canonicalPayload = Encoding.UTF8.GetString(normalizedRequest.CanonicalDigestPayloadUtf8.ToArray());
            Assert.That(canonicalPayload, Does.Not.Contain("requestId"));
            Assert.That(canonicalPayload, Does.Contain("\"protocolVersion\":1"));
            Assert.That(canonicalPayload, Does.Contain("\"steps\""));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenEditRequestIsValid_CompilesToPrimitiveOnlyOperations ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var enemies = new GameObject("Enemies");
            enemies.transform.SetParent(root.transform, worldPositionStays: false);
            var spawner = new GameObject("Spawner");
            spawner.transform.SetParent(enemies.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);

            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "editSpawner",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root/Enemies/Spawner",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "ensureComponent",
                                    type = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                                    @as = "collider",
                                },
                                new
                                {
                                    kind = "set",
                                    target = "$collider",
                                    values = new
                                    {
                                        isTrigger = true,
                                    },
                                },
                            },
                            commit = "context",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);

            var normalizedRequest = result.Request!;
            Assert.That(normalizedRequest.SourceSteps.Count, Is.EqualTo(1));
            Assert.That(normalizedRequest.SourceSteps[0].Kind, Is.EqualTo(IpcRequestStepKind.Edit));
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcRequestStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.CompEnsure,
                    UcliPrimitiveOperationNames.CompSet,
                    UcliPrimitiveOperationNames.SceneSave);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenEditContainsDuplicateCreateAssetActions_AssignsDistinctInternalExecutionKeys ()
        {
            var assetPath = "Assets/Generated/Spawner.asset";
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "createAssets",
                            on = new
                            {
                                project = true,
                            },
                            select = new
                            {
                                projectAsset = new
                                {
                                    path = "ProjectSettings/TagManager.asset",
                                },
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = assetPath,
                                },
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = assetPath,
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            var normalizedRequest = result.Request!;
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(
                    UcliPrimitiveOperationNames.AssetCreate,
                    UcliPrimitiveOperationNames.AssetCreate)
                .AllHavePublicId("createAssets")
                .HaveDistinctInternalExecutionKeys();
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenProjectContextUsesDirectProjectAssetSelection_CompilesProjectAssetSelector ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "setProjectSettings",
                            on = new
                            {
                                project = true,
                            },
                            select = new
                            {
                                projectAsset = new
                                {
                                    path = "ProjectSettings/TagManager.asset",
                                },
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "set",
                                    values = new
                                    {
                                        m_DefaultBehaviorMode = 0,
                                    },
                                },
                            },
                            commit = "context",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            var normalizedRequest = result.Request!;
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(
                    UcliPrimitiveOperationNames.AssetSet,
                    UcliPrimitiveOperationNames.ProjectSave)
                .HasProjectAssetTarget(0, "ProjectSettings/TagManager.asset");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenMultiTargetEditContainsCreateAssetAction_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var spawnerA = new GameObject("SpawnerA");
            spawnerA.transform.SetParent(root.transform, worldPositionStays: false);
            spawnerA.AddComponent<BoxCollider>();
            var spawnerB = new GameObject("SpawnerB");
            spawnerB.transform.SetParent(root.transform, worldPositionStays: false);
            spawnerB.AddComponent<BoxCollider>();
            EditorSceneManager.SaveScene(scene, scenePath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "createAssetForMany",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                from = new
                                {
                                    op = UcliPrimitiveOperationNames.SceneQuery,
                                    args = new
                                    {
                                        pathPrefix = "Root",
                                        componentType = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                                    },
                                },
                                cardinality = "all",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/Multi.asset",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

                var result = new ExecuteRequestNormalizer().Normalize(request);

                Assert.That(result.IsSuccess, Is.True);
                var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
                _ = new ExecuteRequestCompileFailureAssert(error)
                    .HasInvalidArgument("createAssetForMany");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenMultiTargetEditContainsCreatePrefabAction_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var childA = new GameObject("ChildA");
            childA.transform.SetParent(root.transform, worldPositionStays: false);
            var childB = new GameObject("ChildB");
            childB.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "createPrefabForMany",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                from = new
                                {
                                    op = UcliPrimitiveOperationNames.SceneQuery,
                                    args = new
                                    {
                                        pathPrefix = "Root",
                                    },
                                },
                                cardinality = "all",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createObject",
                                    name = "GeneratedChild",
                                    @as = "child",
                                },
                                new
                                {
                                    kind = "createPrefab",
                                    target = "$child",
                                    path = "Assets/Generated/Multi.prefab",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

                var result = new ExecuteRequestNormalizer().Normalize(request);

                Assert.That(result.IsSuccess, Is.True);
                var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
                _ = new ExecuteRequestCompileFailureAssert(error)
                    .HasInvalidArgument("createPrefabForMany");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSelectFromSceneAndLoadedSceneIsDirty_UsesLoadedSceneContents ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            root.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "deletePersistedRoot",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                from = new
                                {
                                    op = UcliPrimitiveOperationNames.SceneQuery,
                                    args = new
                                    {
                                        pathPrefix = "Root",
                                    },
                                },
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            var normalizedRequest = result.Request!;
            var error = CompileSingleStepFailure(normalizedRequest, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("deletePersistedRoot");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSceneEditMutationTargetsClosedScene_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "closedSceneDelete",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("closedSceneDelete");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSceneEditCommitContextTargetsClosedScene_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "closedSceneCommit",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "context",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("closedSceneCommit");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSceneOpenPrecedesClosedSceneEditCommitContext_RuntimeCompileSucceeds ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "op",
                            id = "openScene",
                            op = UcliPrimitiveOperationNames.SceneOpen,
                            args = new
                            {
                                path = scenePath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            id = "closedSceneCommitAfterOpen",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "context",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var compiler = new ExecuteRequestCompiler();
            var executionContext = scope.CreateExecutionContext();
            Assert.That(
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, out _, out var openOperations, out var openError),
                Is.True,
                openError?.Message);
            var openOperation = new SceneOpenOperation();
            var openPlanResult = openOperation.Plan(openOperations[0], executionContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(openPlanResult.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcRequestStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.GoDelete,
                    UcliPrimitiveOperationNames.SceneSave);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSceneEditTargetsLoadedScene_RuntimeCompileAndPlanSucceed ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "loadedSceneDelete",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root/Child",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var executionContext = scope.CreateExecutionContext();
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.GoDelete);
            Assert.That(executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            Assert.That(EditorSceneManager.IsPreviewScene(temporaryScene), Is.True);

            var deleteResult = new GoDeleteOperation().Plan(compiledOperations[0], executionContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(deleteResult.IsSuccess, Is.True, deleteResult.Failure?.Message);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenPrefabEditMutationTargetsClosedPrefab_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");

            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "closedPrefabEnsure",
                            on = new
                            {
                                prefab = prefabPath,
                            },
                            select = new
                            {
                                gameObject = prefabRootName,
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "ensureComponent",
                                    type = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("closedPrefabEnsure");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenPrefabEditTargetsOpenedPrefabStage_RuntimeCompileAndPlanSucceed ()
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "openedPrefabEnsure",
                            on = new
                            {
                                prefab = prefabPath,
                            },
                            select = new
                            {
                                gameObject = prefabRootName,
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "ensureComponent",
                                    type = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var executionContext = scope.CreateExecutionContext();
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.CompEnsure);
            Assert.That(executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabRoot), Is.True);
            Assert.That(temporaryPrefabRoot, Is.Not.Null);
            Assert.That(temporaryPrefabRoot, Is.Not.SameAs(prefabStage.prefabContentsRoot));

            var ensureResult = new CompEnsureOperation().Plan(compiledOperations[0], executionContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(ensureResult.IsSuccess, Is.True, ensureResult.Failure?.Message);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenPrefabOpenPrecedesClosedPrefabEdit_RuntimeCompileSucceeds ()
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");

            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "op",
                            id = "openPrefab",
                            op = UcliPrimitiveOperationNames.PrefabOpen,
                            args = new
                            {
                                path = prefabPath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            id = "closedPrefabEnsureAfterOpen",
                            on = new
                            {
                                prefab = prefabPath,
                            },
                            select = new
                            {
                                gameObject = prefabRootName,
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "ensureComponent",
                                    type = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var compiler = new ExecuteRequestCompiler();
            var executionContext = scope.CreateExecutionContext();
            Assert.That(
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, out _, out var openOperations, out var openError),
                Is.True,
                openError?.Message);
            var openOperation = new PrefabOpenOperation();
            var openPlanResult = openOperation.Plan(openOperations[0], executionContext, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(openPlanResult.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.CompEnsure);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenPlanTokenIsSpecified_TrimsAndStoresPlanToken ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = Array.Empty<object>(),
                });
            request = request with
            {
                PlanToken = "  issued-token  ",
            };

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Request!.PlanToken, Is.EqualTo("issued-token"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenCommandIsValidate_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Validate,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = Array.Empty<object>(),
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRequestJsonKeyOrderDiffers_ProducesStableCanonicalPayload ()
        {
            var requestA = CreateExecuteRequestFromJson(
                UcliCommandIds.Plan,
                "{\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"steps\":[{\"kind\":\"op\",\"id\":\"setSpawner\",\"op\":\"__COMP_SET_OP__\",\"args\":{\"target\":{\"scene\":\"Assets/Scenes/Main.unity\",\"hierarchyPath\":\"Root/Spawner\",\"componentType\":\"UnityEngine.BoxCollider, UnityEngine.PhysicsModule\"},\"sets\":[{\"path\":\"isTrigger\",\"value\":true}]}}]}"
                    .Replace("__COMP_SET_OP__", UcliPrimitiveOperationNames.CompSet, StringComparison.Ordinal));
            var requestB = CreateExecuteRequestFromJson(
                UcliCommandIds.Plan,
                "{\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"steps\":[{\"args\":{\"sets\":[{\"value\":true,\"path\":\"isTrigger\"}],\"target\":{\"componentType\":\"UnityEngine.BoxCollider, UnityEngine.PhysicsModule\",\"hierarchyPath\":\"Root/Spawner\",\"scene\":\"Assets/Scenes/Main.unity\"}},\"op\":\"__COMP_SET_OP__\",\"id\":\"setSpawner\",\"kind\":\"op\"}],\"protocolVersion\":1}"
                    .Replace("__COMP_SET_OP__", UcliPrimitiveOperationNames.CompSet, StringComparison.Ordinal));

            var normalizer = new ExecuteRequestNormalizer();
            var resultA = normalizer.Normalize(requestA);
            var resultB = normalizer.Normalize(requestB);

            Assert.That(resultA.IsSuccess, Is.True);
            Assert.That(resultB.IsSuccess, Is.True);
            Assert.That(resultA.Request!.CanonicalDigestPayloadUtf8.Span.SequenceEqual(resultB.Request!.CanonicalDigestPayloadUtf8.Span), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenProtocolVersionMismatches_ReturnsProtocolVersionMismatchError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                new
                {
                    protocolVersion = 999,
                    requestId = RequestId,
                    steps = Array.Empty<object>(),
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.ProtocolVersionMismatch));
            Assert.That(result.Error.OpId, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRequestIdIsInvalid_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = "invalid",
                    steps = Array.Empty<object>(),
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenTopLevelContainsUnknownProperty_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Resolve,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = Array.Empty<object>(),
                    unknown = true,
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenStepIdIsDuplicated_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            id = "same",
                            op = UcliPrimitiveOperationNames.ProjectRefresh,
                            args = new { },
                        },
                        new
                        {
                            kind = "op",
                            id = "same",
                            op = UcliPrimitiveOperationNames.ProjectRefresh,
                            args = new { },
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "same");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOpArgsPropertyIsMissing_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            id = "missingArgs",
                            op = UcliPrimitiveOperationNames.SceneOpen,
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "missingArgs");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOpArgsPropertyIsNotObject_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            id = "argsType",
                            op = UcliPrimitiveOperationNames.SceneOpen,
                            args = Array.Empty<object>(),
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "argsType");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenEditCommitIsMissing_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "missingCommit",
                            on = new
                            {
                                scene = "Assets/Scenes/Main.unity",
                            },
                            select = new
                            {
                                gameObject = "Root",
                                cardinality = "one",
                            },
                            actions = Array.Empty<object>(),
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "missingCommit");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSelectFromIsUsedOutsideSceneContext_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = RequestId,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "prefabFrom",
                            on = new
                            {
                                prefab = "Assets/Prefabs/Enemy.prefab",
                            },
                            select = new
                            {
                                from = new
                                {
                                    op = UcliPrimitiveOperationNames.SceneQuery,
                                    args = new
                                    {
                                        pathPrefix = "Root",
                                    },
                                },
                                cardinality = "all",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "context",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "prefabFrom");
        }

        private static (NormalizedRequestStep Step, IReadOnlyList<NormalizedOperation> Operations) CompileSingleStep (
            NormalizedExecuteRequest request,
            int stepIndex)
        {
            using var executionContext = new OperationExecutionContext();
            return CompileSingleStep(request, stepIndex, executionContext);
        }

        private static (NormalizedRequestStep Step, IReadOnlyList<NormalizedOperation> Operations) CompileSingleStep (
            NormalizedExecuteRequest request,
            int stepIndex,
            OperationExecutionContext executionContext)
        {
            var compiler = new ExecuteRequestCompiler();
            var sourceStep = request.SourceSteps[stepIndex];
            Assert.That(
                compiler.TryCompileExecutionStep(sourceStep, executionContext, out var compiledStep, out var compiledOperations, out var error),
                Is.True,
                error?.Message);

            return (compiledStep, compiledOperations);
        }

        private static ExecuteRequestNormalizationError CompileSingleStepFailure (
            NormalizedExecuteRequest request,
            int stepIndex,
            OperationExecutionContext executionContext)
        {
            var compiler = new ExecuteRequestCompiler();
            var sourceStep = request.SourceSteps[stepIndex];
            Assert.That(
                compiler.TryCompileExecutionStep(sourceStep, executionContext, out _, out _, out var error),
                Is.False);
            Assert.That(error, Is.Not.Null);
            return error;
        }

        private static void AssertInvalidArgument (
            ExecuteRequestNormalizationResult result,
            string? expectedOpId = null)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(result.Error.OpId, Is.EqualTo(expectedOpId));
        }

        private static IpcExecuteRequest CreateExecuteRequest (string command, object arguments)
        {
            var argumentsJson = JsonSerializer.Serialize(arguments);
            return CreateExecuteRequestFromJson(command, argumentsJson);
        }

        private static IpcExecuteRequest CreateExecuteRequestFromJson (string command, string argumentsJson)
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return new IpcExecuteRequest(
                Command: command,
                Arguments: document.RootElement.Clone());
        }

    }
}
