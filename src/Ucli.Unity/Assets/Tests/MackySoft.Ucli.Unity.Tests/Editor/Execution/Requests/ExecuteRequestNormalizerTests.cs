using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Index;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

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
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            Assert.That(compiledOperations[0].AllowRequestLocalAliases, Is.False);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKindNames.Operation,
                    null,
                    false,
                    IpcExecuteExpectedPostStateNames.Unavailable);

            var canonicalPayload = Encoding.UTF8.GetString(normalizedRequest.CanonicalDigestPayloadUtf8.ToArray());
            Assert.That(canonicalPayload, Does.Not.Contain("requestId"));
            Assert.That(canonicalPayload, Does.Contain("\"protocolVersion\":1"));
            Assert.That(canonicalPayload, Does.Contain("\"steps\""));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenProjectRefreshOpRequestIsValid_CompilesRefreshPostReadSourceStep ()
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
                            id = "refresh",
                            op = UcliPrimitiveOperationNames.ProjectRefresh,
                            args = new
                            {
                            },
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);

            var normalizedRequest = result.Request!;
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.ProjectRefresh)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKindNames.Refresh,
                    null,
                    true,
                    IpcExecuteExpectedPostStateNames.Unavailable);
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
                    UcliPrimitiveOperationNames.SceneSave)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKindNames.Edit,
                    IpcExecutePostReadCommitNames.Context,
                    true,
                    IpcExecuteExpectedPostStateNames.Deterministic);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSelectFromUsesFirst_SelectsFirstHierarchyTraversalMatch ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var zRoot = new GameObject("ZRoot");
            var zChild = new GameObject("ZChild");
            zChild.transform.SetParent(zRoot.transform, worldPositionStays: false);
            _ = new GameObject("ARoot");
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
                            id = "deleteFirst",
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
                                    },
                                },
                                cardinality = "first",
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
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit", UcliPrimitiveOperationNames.GoDelete)
                .AllHavePublicId("deleteFirst")
                .HaveDistinctInternalExecutionKeys();
            var target = compiledOperations[0].Args.GetProperty("target");
            Assert.That(target.GetProperty("hierarchyPath").GetString(), Is.EqualTo("ZRoot"));
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
                    UcliPrimitiveOperationNames.ProjectSave);
            var target = compiledOperations[0].Args.GetProperty("target");
            Assert.That(target.GetProperty("projectAssetPath").GetString(), Is.EqualTo("ProjectSettings/TagManager.asset"));
            Assert.That(target.TryGetProperty("assetPath", out _), Is.False);
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
                    .HasInvalidArgument("createAssetForMany")
                    .HasMessageContaining("requires the selection to resolve to at most one target.");
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
                .HasInvalidArgument("createPrefabForMany")
                .HasMessageContaining("requires the selection to resolve to at most one target.");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenPrefabEditContainsCreatePrefabAction_ReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
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
                            id = "createPrefabInPrefabContext",
                            on = new
                            {
                                prefab = prefabPath,
                            },
                            select = new
                            {
                                gameObject = "PrefabRoot",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createPrefab",
                                    path = "Assets/Generated/Nested.prefab",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "createPrefabInPrefabContext");
            Assert.That(result.Error!.Message, Does.Contain("requires both 'target' and 'path'."));
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
                .HasInvalidArgument("deletePersistedRoot")
                .HasMessageContaining("cardinality 'one' requires exactly one target.");
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenSelectFromSceneTargetsDirtyLoadedScene_RuntimeCompileAndPlanSucceed () => UniTask.ToCoroutine(async () =>
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
                            id = "ensureDirtySceneTarget",
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
                                        pathPrefix = "Renamed",
                                    },
                                },
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
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit", UcliPrimitiveOperationNames.CompEnsure)
                .AllHavePublicId("ensureDirtySceneTarget")
                .HaveDistinctInternalExecutionKeys();
            Assert.That(executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            Assert.That(
                temporaryScene.GetRootGameObjects(),
                Has.Some.Matches<GameObject>(gameObject => gameObject.name == "Renamed"));

            var ensureResult = await new CompEnsureOperation().PlanAsync(compiledOperations[0], executionContext, CancellationToken.None);

            Assert.That(ensureResult.IsSuccess, Is.True, ensureResult.Failure?.Message);
        });

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSelectFromSkipsSlashNamedGameObjects_CompiledStepCarriesPartialCoverageDiagnostic ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("GoodRoot");
            _ = new GameObject("Bad/Root");
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
                            id = "deleteGoodRoot",
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
                                        pathPrefix = "GoodRoot",
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
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit", UcliPrimitiveOperationNames.GoDelete)
                .AllHavePublicId("deleteGoodRoot")
                .HaveDistinctInternalExecutionKeys();
            Assert.That(compiledStep.Diagnostics.Count, Is.EqualTo(1));
            var diagnostic = compiledStep.Diagnostics[0];
            Assert.That(diagnostic.Code, Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects));
            Assert.That(diagnostic.Severity, Is.EqualTo(IpcExecuteDiagnosticSeverityNames.Warning));
            Assert.That(diagnostic.CoverageImpact, Is.EqualTo(IpcExecuteDiagnosticCoverageImpactNames.Partial));
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
                .HasInvalidArgument("closedSceneDelete")
                .HasMessageContaining("Add 'ucli.scene.open' before this step.");
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
                .HasInvalidArgument("closedSceneCommit")
                .HasMessageContaining("Add 'ucli.scene.open' before this step.");
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenSceneOpenPrecedesClosedSceneEditCommitContext_RuntimeCompileSucceeds () => UniTask.ToCoroutine(async () =>
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
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openOperation = new SceneOpenOperation();
            var openPlanResult = await openOperation.PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcRequestStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.GoDelete,
                    UcliPrimitiveOperationNames.SceneSave);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenSceneEditTargetsLoadedScene_RuntimeCompileAndPlanSucceed () => UniTask.ToCoroutine(async () =>
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
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit", UcliPrimitiveOperationNames.GoDelete)
                .AllHavePublicId("loadedSceneDelete")
                .HaveDistinctInternalExecutionKeys();
            Assert.That(executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            Assert.That(EditorSceneManager.IsPreviewScene(temporaryScene), Is.True);

            var deleteResult = await new GoDeleteOperation().PlanAsync(compiledOperations[0], executionContext, CancellationToken.None);

            Assert.That(deleteResult.IsSuccess, Is.True, deleteResult.Failure?.Message);
        });

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
                .HasInvalidArgument("closedPrefabEnsure")
                .HasMessageContaining("Add 'ucli.prefab.open' before this step.");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedPrefabCreateAssetUsesContextCommit_RuntimeCompileReturnsInvalidArgumentError ()
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
                            id = "closedPrefabCreateAssetWithCommit",
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
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/FromClosedPrefab.asset",
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
                .HasInvalidArgument("closedPrefabCreateAssetWithCommit")
                .HasMessageContaining("Add 'ucli.prefab.open' before this step.");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedPrefabOptionalSelectionDoesNotResolveAndCommitIsContext_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
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
                            id = "closedPrefabOptionalCreateAssetWithCommit",
                            on = new
                            {
                                prefab = prefabPath,
                            },
                            select = new
                            {
                                gameObject = "Missing",
                                cardinality = "atMostOne",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/FromClosedOptionalPrefab.asset",
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
                .HasInvalidArgument("closedPrefabOptionalCreateAssetWithCommit")
                .HasMessageContaining("Add 'ucli.prefab.open' before this step.");
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenPrefabEditTargetsDirtyOpenedPrefabStage_RuntimeCompileAndPlanSucceed () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot", "Child");
            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            prefabStage!.prefabContentsRoot.transform.GetChild(0).name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
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
                                gameObject = $"{prefabRootName}/Renamed",
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
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit", UcliPrimitiveOperationNames.CompEnsure)
                .AllHavePublicId("openedPrefabEnsure")
                .HaveDistinctInternalExecutionKeys();
            Assert.That(executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabRoot), Is.True);
            Assert.That(temporaryPrefabRoot, Is.Not.Null);
            Assert.That(temporaryPrefabRoot, Is.Not.SameAs(prefabStage.prefabContentsRoot));
            Assert.That(temporaryPrefabRoot!.transform.GetChild(0).name, Is.EqualTo("Renamed"));

            var ensureResult = await new CompEnsureOperation().PlanAsync(compiledOperations[0], executionContext, CancellationToken.None);

            Assert.That(ensureResult.IsSuccess, Is.True, ensureResult.Failure?.Message);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenPrefabOpenPrecedesClosedPrefabEdit_RuntimeCompileSucceeds () => UniTask.ToCoroutine(async () =>
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
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openOperation = new PrefabOpenOperation();
            var openPlanResult = await openOperation.PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit", UcliPrimitiveOperationNames.CompEnsure)
                .AllHavePublicId("closedPrefabEnsureAfterOpen")
                .HaveDistinctInternalExecutionKeys();
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenPrefabOpenPrecedesClosedPrefabCreateAssetWithContextCommit_RuntimeCompileSucceeds () => UniTask.ToCoroutine(async () =>
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
                            id = "openPrefabForCommit",
                            op = UcliPrimitiveOperationNames.PrefabOpen,
                            args = new
                            {
                                path = prefabPath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            id = "closedPrefabCreateAssetAfterOpen",
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
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/FromOpenedPrefab.asset",
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
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openPlanResult = await new PrefabOpenOperation().PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True, openPlanResult.Failure?.Message);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcRequestStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.AssetCreate,
                    UcliPrimitiveOperationNames.PrefabSave)
                .AllHavePublicId("closedPrefabCreateAssetAfterOpen")
                .HaveDistinctInternalExecutionKeys();
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenOpenedPrefabOptionalSelectionDoesNotResolveAndCommitIsContext_RuntimeCompileLowersPrefabSaveOnly () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
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
                            id = "openPrefabForOptionalCommit",
                            op = UcliPrimitiveOperationNames.PrefabOpen,
                            args = new
                            {
                                path = prefabPath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            id = "openedPrefabOptionalCommit",
                            on = new
                            {
                                prefab = prefabPath,
                            },
                            select = new
                            {
                                gameObject = "Missing",
                                cardinality = "atMostOne",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/FromOpenedOptionalPrefab.asset",
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
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openPlanResult = await new PrefabOpenOperation().PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True, openPlanResult.Failure?.Message);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcRequestStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.PrefabSave)
                .AllHavePublicId("openedPrefabOptionalCommit")
                .HaveDistinctInternalExecutionKeys();
        });

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenDirectSceneSelectionDoesNotResolveAndCardinalityIsOne_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
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
                            id = "missingDirectSelection",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root/Missing",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/DirectMissing.asset",
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
                .HasInvalidArgument("missingDirectSelection")
                .HasMessageContaining("cardinality 'one' requires exactly one target.");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedSceneOptionalSelectionDoesNotResolveAndCommitIsNone_RuntimeCompileSucceedsWithNoOperations ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
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
                            id = "optionalMissingDirectSelectionNoCommit",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root/Missing",
                                cardinality = "atMostOne",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/OptionalMissingNoCommit.asset",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit")
                .AllHavePublicId("optionalMissingDirectSelectionNoCommit");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedSceneOptionalSelectionDoesNotResolveAndCommitIsProject_RuntimeCompileSucceedsWithNoOperations ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
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
                            id = "optionalMissingDirectSelectionProjectCommit",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root/Missing",
                                cardinality = "atMostOne",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/OptionalMissingProjectCommit.asset",
                                },
                            },
                            commit = "project",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcRequestStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.ProjectSave)
                .AllHavePublicId("optionalMissingDirectSelectionProjectCommit");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedSceneOptionalSelectionDoesNotResolveAndCommitIsContext_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
                            id = "optionalMissingDirectSelectionSceneContextCommit",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root/Missing",
                                cardinality = "atMostOne",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/OptionalMissingSceneContextCommit.asset",
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
                .HasInvalidArgument("optionalMissingDirectSelectionSceneContextCommit")
                .HasMessageContaining("Add 'ucli.scene.open' before this step.");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedSceneOptionalSelectionDoesNotResolveAndCommitIsNone_DoesNotRetainImplicitPreviewState ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
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
                            id = "optionalMissingDirectSelectionNoCommitReleaseScenePreview",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root/Missing",
                                cardinality = "atMostOne",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/OptionalMissingNoCommit.asset",
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
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit")
                .AllHavePublicId("optionalMissingDirectSelectionNoCommitReleaseScenePreview");
            Assert.That(executionContext.TryGetTemporaryScene(scenePath, out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedPrefabOptionalSelectionDoesNotResolveAndCommitIsNone_DoesNotRetainImplicitPreviewState ()
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
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
                            id = "optionalMissingDirectSelectionNoCommitReleasePrefabPreview",
                            on = new
                            {
                                prefab = prefabPath,
                            },
                            select = new
                            {
                                gameObject = "Missing",
                                cardinality = "atMostOne",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/OptionalMissingNoCommitPrefab.asset",
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
                .HasLoweredOperations(IpcRequestStepKind.Edit, "edit")
                .AllHavePublicId("optionalMissingDirectSelectionNoCommitReleasePrefabPreview");
            Assert.That(executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedSceneDirectSelectionTargetsPersistedObjectForCreateAsset_RuntimeCompileSucceeds ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
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
                            id = "closedSceneCreateAsset",
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
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/FromClosedScene.asset",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.AssetCreate)
                .AllHavePublicId("closedSceneCreateAsset");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenClosedPrefabDirectSelectionTargetsPersistedObjectForCreateAsset_RuntimeCompileSucceeds ()
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
                            id = "closedPrefabCreateAsset",
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
                                    kind = "createAsset",
                                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                                    path = "Assets/Generated/FromClosedPrefab.asset",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.AssetCreate)
                .AllHavePublicId("closedPrefabCreateAsset");
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
        public void Normalize_WhenAllowDangerousIsSpecified_StoresAllowDangerous ()
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
                AllowDangerous = true,
            };

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Request!.AllowDangerous, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeIsSpecified_RejectsRawOperationStep ()
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
                            id = "rawSet",
                            op = UcliPrimitiveOperationNames.CompSet,
                            args = new { },
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = new ExecuteRequestNormalizer().Normalize(request);

            AssertInvalidArgument(result, "rawSet");
            Assert.That(result.Error!.Message, Is.EqualTo("Play Mode mutation requests support only public edit steps."));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeSceneCommitIsContext_ReturnsPersistenceForbiddenError ()
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
                            id = "playSceneCommit",
                            on = new
                            {
                                scene = "Assets/Scenes/Main.unity",
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
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(PlayModeErrorCodes.PlayModePersistenceForbidden));
            Assert.That(result.Error.OpId, Is.EqualTo("playSceneCommit"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeSceneCommitIsNone_CompilesLiveMutationPostReadSource ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
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
                            id = "playSceneMutation",
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
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: true);
            Assert.That(compiledOperations, Has.Count.EqualTo(1));
            Assert.That(compiledOperations[0].SuppressPersistenceReporting, Is.True);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKindNames.Edit,
                    IpcExecutePostReadCommitNames.None,
                    false,
                    IpcExecuteExpectedPostStateNames.Unavailable,
                    expectedPlayModeMutation: true);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeAssetCommitIsContext_CompilesTargetLimitedAssetSave ()
        {
            using var scope = new EditorTestScope();
            _ = scope.CreateScriptableAsset<AssetOperationTestAsset>(nameof(ExecuteRequestNormalizerTests), out var assetPath);
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
                            id = "playAssetMutation",
                            on = new
                            {
                                asset = assetPath,
                            },
                            select = new
                            {
                                self = true,
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "set",
                                    values = new
                                    {
                                        text = "updated",
                                    },
                                },
                            },
                            commit = "context",
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (_, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: true);
            Assert.That(compiledOperations.Select(static operation => operation.Op), Is.EqualTo(new[]
            {
                UcliPrimitiveOperationNames.AssetSet,
                UcliPrimitiveOperationNames.AssetSave,
            }));
            Assert.That(compiledOperations[1].Args.GetProperty("target").GetProperty("assetPath").GetString(), Is.EqualTo(assetPath));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRevertPrefabOverridesRunsOutsidePlayMode_DoesNotSuppressPersistenceReporting ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            _ = root.AddComponent<BoxCollider>();
            EditorSceneManager.SaveScene(scene, scenePath);
            var request = CreatePrefabOverrideRevertRequest(scenePath, allowPlayMode: false);

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (_, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: false);
            Assert.That(compiledOperations, Has.Count.EqualTo(1));
            Assert.That(compiledOperations[0].Op, Is.EqualTo(UcliPrimitiveOperationNames.PrefabRevertOverrides));
            Assert.That(compiledOperations[0].SuppressPersistenceReporting, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRevertPrefabOverridesRunsInPlayMode_SuppressesPersistenceReporting ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            _ = root.AddComponent<BoxCollider>();
            EditorSceneManager.SaveScene(scene, scenePath);
            var request = CreatePrefabOverrideRevertRequest(scenePath, allowPlayMode: true);

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (_, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: true);
            Assert.That(compiledOperations, Has.Count.EqualTo(1));
            Assert.That(compiledOperations[0].Op, Is.EqualTo(UcliPrimitiveOperationNames.PrefabRevertOverrides));
            Assert.That(compiledOperations[0].SuppressPersistenceReporting, Is.True);
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
            Assert.That(result.Error!.Code, Is.EqualTo(IpcProtocolErrorCodes.ProtocolVersionMismatch));
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

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenEditCommitLiteralIsUnsupported_ReturnsDetailedInvalidArgumentError ()
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
                            id = "badCommit",
                            on = new
                            {
                                scene = "Assets/Scenes/Main.unity",
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
                            commit = "later",
                        },
                    },
                });

            var result = new ExecuteRequestNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Error.OpId, Is.EqualTo("badCommit"));
            Assert.That(result.Error.Message, Is.EqualTo("Edit step property 'step.commit' must be one of 'none', 'context', or 'project'."));
        }

        private static IpcExecuteRequest CreatePrefabOverrideRevertRequest (
            string scenePath,
            bool allowPlayMode)
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
                            id = "revertOverrides",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "Root",
                                component = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "revertPrefabOverrides",
                                    targetAssetPath = "Assets/Prefabs/Enemy.prefab",
                                },
                            },
                            commit = "none",
                        },
                    },
                });
            return request with
            {
                AllowPlayMode = allowPlayMode,
            };
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
            return CompileSingleStep(request, stepIndex, executionContext, allowPlayMode: false);
        }

        private static (NormalizedRequestStep Step, IReadOnlyList<NormalizedOperation> Operations) CompileSingleStep (
            NormalizedExecuteRequest request,
            int stepIndex,
            OperationExecutionContext executionContext,
            bool allowPlayMode)
        {
            var compiler = new ExecuteRequestCompiler();
            var sourceStep = request.SourceSteps[stepIndex];
            Assert.That(
                compiler.TryCompileExecutionStep(sourceStep, executionContext, allowPlayMode, out var compiledStep, out var compiledOperations, out _, out var error),
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
                compiler.TryCompileExecutionStep(sourceStep, executionContext, out _, out _, out _, out var error),
                Is.False);
            Assert.That(error, Is.Not.Null);
            return error;
        }

        private static void AssertInvalidArgument (
            ExecuteRequestNormalizationResult result,
            string expectedOpId = null)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
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
