using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Operations;
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
        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOpRequestIsValid_ReturnsNormalizedRequestAndCanonicalPayload ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.Resolve,
                            args = new
                            {
                                scene = "Assets/Scenes/Main.unity",
                                hierarchyPath = "Root/Enemies/Spawner",
                            },
                        },
                    },
                });

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);

            var normalizedRequest = result.Request!;
            Assert.That(normalizedRequest.SourceSteps.Count, Is.EqualTo(1));
            Assert.That(normalizedRequest.SourceSteps[0].Kind, Is.EqualTo(IpcExecuteStepKind.Op));
            Assert.That(normalizedRequest.SourceSteps[0].OperationName, Is.EqualTo(UcliPrimitiveOperationNames.Resolve));
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            Assert.That(compiledOperations[0].AllowRequestLocalAliases, Is.False);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Operation,
                    null,
                    false,
                    IpcExecuteExpectedPostState.Unavailable);

            var canonicalPayload = Encoding.UTF8.GetString(normalizedRequest.CanonicalDigestPayloadUtf8.ToArray());
            Assert.That(canonicalPayload, Does.Contain("\"protocolVersion\":1"));
            Assert.That(canonicalPayload, Does.Contain("\"steps\""));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenProjectRefreshOpRequestIsValid_CompilesRefreshPostReadSourceStep ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.ProjectRefresh,
                            args = new
                            {
                            },
                        },
                    },
                });

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);

            var normalizedRequest = result.Request!;
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.ProjectRefresh)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Refresh,
                    null,
                    true,
                    IpcExecuteExpectedPostState.Unavailable);
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Enemies/Spawner",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);

            var normalizedRequest = result.Request!;
            Assert.That(normalizedRequest.SourceSteps.Count, Is.EqualTo(1));
            Assert.That(normalizedRequest.SourceSteps[0].Kind, Is.EqualTo(IpcExecuteStepKind.Edit));
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcExecuteStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.CompEnsure,
                    UcliPrimitiveOperationNames.CompSet,
                    UcliPrimitiveOperationNames.SceneSave)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Edit,
                    IpcExecutePostReadCommit.Context,
                    true,
                    IpcExecuteExpectedPostState.Deterministic);
            var producedAlias = compiledOperations[0].As;
            var referencedAlias = compiledOperations[1].AliasReferences.Resolve(new UcliPlanAlias("collider"));
            Assert.That(compiledOperations[0].AllowRequestLocalAliases, Is.True);
            Assert.That(compiledOperations[1].AllowRequestLocalAliases, Is.True);
            Assert.That(producedAlias, Is.TypeOf<RequestLocalAliasIdentity.EditActionAliasIdentity>());
            Assert.That(referencedAlias, Is.EqualTo(producedAlias));
            Assert.That(
                compiledOperations[1].Args.GetProperty("target").GetProperty("var").GetString(),
                Is.EqualTo("collider"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenEditRequestReparentsDirectSceneSelection_CompilesGoReparentOperation ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var newParent = new GameObject("NewParent");
            newParent.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);

            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Child",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "reparent",
                                    parent = "Root/NewParent",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            var normalizedRequest = result.Request!;
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcExecuteStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.GoReparent)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Edit,
                    IpcExecutePostReadCommit.None,
                    false,
                    IpcExecuteExpectedPostState.Deterministic);

            var args = compiledOperations[0].Args;
            var target = args.GetProperty("target");
            Assert.That(target.GetProperty("scene").GetString(), Is.EqualTo(scenePath));
            Assert.That(target.GetProperty("hierarchyPath").GetString(), Is.EqualTo("Root/Child"));
            var parent = args.GetProperty("parent");
            Assert.That(parent.GetProperty("scene").GetString(), Is.EqualTo(scenePath));
            Assert.That(parent.GetProperty("hierarchyPath").GetString(), Is.EqualTo("Root/NewParent"));
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit", UcliPrimitiveOperationNames.GoDelete)
                .AllBelongToSourceStep()
                .HaveDistinctExecutionKeys();
            var target = compiledOperations[0].Args.GetProperty("target");
            Assert.That(target.GetProperty("hierarchyPath").GetString(), Is.EqualTo("ZRoot"));
        }

        [Test]
        [Category("Size.Small")]
        public void Compile_WhenSelectFromComponentTypeCannotResolve_ReturnsInvalidArgumentBeforeSelection ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
                                        componentType = "Missing.Component, Missing.Assembly",
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
                            commit = "none",
                        },
                    },
                });

            var normalizationResult = CreateNormalizer().Normalize(request);

            Assert.That(normalizationResult.IsSuccess, Is.True, normalizationResult.Error?.Message);
            using var executionContext = scope.CreateExecutionContext();
            var error = CompileSingleStepFailure(normalizationResult.Request!, 0, executionContext);
            Assert.That(error.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(error.InstancePath, Is.EqualTo("/steps/0"));
            Assert.That(error.Message, Does.Contain("TypeId could not be resolved"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenEditContainsDuplicateCreateAssetActions_AssignsDistinctExecutionKeys ()
        {
            var assetPath = "Assets/Generated/Spawner.asset";
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "project",
                            },
                            select = new
                            {
                                kind = "projectAsset",
                                path = "ProjectSettings/TagManager.asset",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            var normalizedRequest = result.Request!;
            var (compiledStep, compiledOperations) = CompileSingleStep(normalizedRequest, 0);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(
                    UcliPrimitiveOperationNames.AssetCreate,
                    UcliPrimitiveOperationNames.AssetCreate)
                .AllBelongToSourceStep()
                .HaveDistinctExecutionKeys();
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenProjectContextUsesDirectProjectAssetSelection_CompilesProjectAssetSelector ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "project",
                            },
                            select = new
                            {
                                kind = "projectAsset",
                                path = "ProjectSettings/TagManager.asset",
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

            var result = CreateNormalizer().Normalize(request);

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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
                                        pathPrefix = "Root",
                                        componentType = "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
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

                var result = CreateNormalizer().Normalize(request);

                Assert.That(result.IsSuccess, Is.True);
                var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
                _ = new ExecuteRequestCompileFailureAssert(error)
                    .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
                                        pathPrefix = "Root",
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

                var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
                .HasMessageContaining("requires the selection to resolve to at most one target.");
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOpenedPrefabEditContainsCreatePrefabAction_RuntimeCompileReturnsInvalidArgumentError ()
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = prefabRootName,
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
                .HasMessageContaining("requires a GameObject target in scene context.");
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
                                        pathPrefix = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
            var normalizedRequest = result.Request!;
            var error = CompileSingleStepFailure(normalizedRequest, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
                                        pathPrefix = "Renamed",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var executionContext = scope.CreateExecutionContext();
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit", UcliPrimitiveOperationNames.CompEnsure)
                .AllBelongToSourceStep()
                .HaveDistinctExecutionKeys();
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
                                        pathPrefix = "GoodRoot",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit", UcliPrimitiveOperationNames.GoDelete)
                .AllBelongToSourceStep()
                .HaveDistinctExecutionKeys();
            Assert.That(compiledStep.Diagnostics.Count, Is.EqualTo(1));
            var diagnostic = compiledStep.Diagnostics[0];
            Assert.That(diagnostic.Code, Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects));
            Assert.That(diagnostic.Severity, Is.EqualTo(UcliDiagnosticSeverity.Warning));
            Assert.That(diagnostic.CoverageImpact, Is.EqualTo(IpcExecuteDiagnosticCoverageImpact.Partial));
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
                .HasMessageContaining("Add 'ucli.scene.open' before this step.");
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenRawSceneOpenPrecedesClosedSceneEditCommitContext_RuntimeCompileStillRequiresLiveScene () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.SceneOpen,
                            args = new
                            {
                                path = scenePath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var compiler = CreateCompiler();
            var executionContext = scope.CreateExecutionContext();
            Assert.That(
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, allowPlayMode: false, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openOperation = new SceneOpenOperation();
            var openPlanResult = await openOperation.PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True);
            Assert.That(executionContext.HasPlannedLiveSceneOpen(scenePath), Is.False);
            var error = CompileSingleStepFailure(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/1")
                .HasMessageContaining("Add 'ucli.scene.open' before this step.");
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Child",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var executionContext = scope.CreateExecutionContext();
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit", UcliPrimitiveOperationNames.GoDelete)
                .AllBelongToSourceStep()
                .HaveDistinctExecutionKeys();
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = prefabRootName,
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = prefabRootName,
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = $"{prefabRootName}/Renamed",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var executionContext = scope.CreateExecutionContext();
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit", UcliPrimitiveOperationNames.CompEnsure)
                .AllBelongToSourceStep()
                .HaveDistinctExecutionKeys();
            Assert.That(executionContext.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryPrefabRoot), Is.True);
            Assert.That(temporaryPrefabRoot, Is.Not.Null);
            Assert.That(temporaryPrefabRoot, Is.Not.SameAs(prefabStage.prefabContentsRoot));
            Assert.That(temporaryPrefabRoot!.transform.GetChild(0).name, Is.EqualTo("Renamed"));

            var ensureResult = await new CompEnsureOperation().PlanAsync(compiledOperations[0], executionContext, CancellationToken.None);

            Assert.That(ensureResult.IsSuccess, Is.True, ensureResult.Failure?.Message);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenRawPrefabOpenPrecedesClosedPrefabEdit_RuntimeCompileStillRequiresOpenPrefab () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");

            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.PrefabOpen,
                            args = new
                            {
                                path = prefabPath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = prefabRootName,
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var compiler = CreateCompiler();
            var executionContext = scope.CreateExecutionContext();
            Assert.That(
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, allowPlayMode: false, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openOperation = new PrefabOpenOperation();
            var openPlanResult = await openOperation.PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True);
            Assert.That(executionContext.HasPlannedLivePrefabOpen(prefabPath), Is.False);
            var error = CompileSingleStepFailure(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/1")
                .HasMessageContaining("Add 'ucli.prefab.open' before this step.");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenRawPrefabOpenPrecedesClosedPrefabCreateAssetWithContextCommit_RuntimeCompileStillRequiresOpenPrefab () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");

            var prefabRootName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.PrefabOpen,
                            args = new
                            {
                                path = prefabPath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = prefabRootName,
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var compiler = CreateCompiler();
            var executionContext = scope.CreateExecutionContext();
            Assert.That(
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, allowPlayMode: false, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openPlanResult = await new PrefabOpenOperation().PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True, openPlanResult.Failure?.Message);
            Assert.That(executionContext.HasPlannedLivePrefabOpen(prefabPath), Is.False);
            var error = CompileSingleStepFailure(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/1")
                .HasMessageContaining("Add 'ucli.prefab.open' before this step.");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Normalize_WhenRawPrefabOpenPrecedesOptionalPrefabCommit_RuntimeCompileStillRequiresOpenPrefab () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.PrefabOpen,
                            args = new
                            {
                                path = prefabPath,
                            },
                        },
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var compiler = CreateCompiler();
            var executionContext = scope.CreateExecutionContext();
            Assert.That(
                compiler.TryCompileExecutionStep(result.Request!.SourceSteps[0], executionContext, allowPlayMode: false, out _, out var openOperations, out _, out var openError),
                Is.True,
                openError?.Message);
            var openPlanResult = await new PrefabOpenOperation().PlanAsync(openOperations[0], executionContext, CancellationToken.None);

            Assert.That(openPlanResult.IsSuccess, Is.True, openPlanResult.Failure?.Message);
            Assert.That(executionContext.HasPlannedLivePrefabOpen(prefabPath), Is.False);
            var error = CompileSingleStepFailure(result.Request, 1, executionContext);
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/1")
                .HasMessageContaining("Add 'ucli.prefab.open' before this step.");
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit")
                .AllBelongToSourceStep();
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(
                    IpcExecuteStepKind.Edit,
                    "edit",
                    UcliPrimitiveOperationNames.ProjectSave)
                .AllBelongToSourceStep();
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var error = CompileSingleStepFailure(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompileFailureAssert(error)
                .HasInvalidArgument("/steps/0")
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root/Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var executionContext = scope.CreateExecutionContext();
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit")
                .AllBelongToSourceStep();
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Missing",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var executionContext = scope.CreateExecutionContext();
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, executionContext);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Edit, "edit")
                .AllBelongToSourceStep();
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.AssetCreate)
                .AllBelongToSourceStep();
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = prefabPath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = prefabRootName,
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext());
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasOperationNames(UcliPrimitiveOperationNames.AssetCreate)
                .AllBelongToSourceStep();
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenPlanTokenIsSpecified_TrimsAndStoresPlanToken ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = Array.Empty<object>(),
                });
            request = request with
            {
                PlanToken = "  issued-token  ",
            };

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Request!.PlanToken, Is.EqualTo("issued-token"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowDangerousIsSpecified_StoresAllowDangerous ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = Array.Empty<object>(),
                });
            request = request with
            {
                AllowDangerous = true,
            };

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Request!.AllowDangerous, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeIsSpecified_RejectsDisallowedRawOperationStep ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.CompSet,
                            args = new { },
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = CreateNormalizer().Normalize(request);

            AssertInvalidArgument(result, "/steps/0");
            Assert.That(result.Error!.Message, Is.EqualTo($"Operation '{UcliPrimitiveOperationNames.CompSet}' does not support Play Mode execution."));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeIsSpecified_AllowsCsEvalRawOperationStep ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            op = UcliPrimitiveOperationNames.CsEval,
                            args = new
                            {
                                source = "context.DeclareNoTouchedResources(); return 1;",
                            },
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, new OperationExecutionContext(), allowPlayMode: true);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Op, UcliPrimitiveOperationNames.CsEval, UcliPrimitiveOperationNames.CsEval)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Operation,
                    null,
                    false,
                IpcExecuteExpectedPostState.Unavailable);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOperationRequiresPlayModeWithoutAllowPlayMode_ReturnsInvalidArgument ()
        {
            const string operationName = "game.cheat.required";
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            op = operationName,
                            args = new { },
                        },
                    },
                });

            var result = CreateNormalizer(CreatePlayModeOperation(operationName, UcliOperationPlayModeSupport.Required)).Normalize(request);

            AssertInvalidArgument(result, "/steps/0");
            Assert.That(result.Error!.Message, Is.EqualTo($"Operation '{operationName}' requires --allowPlayMode."));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenOperationRequiresPlayModeWithAllowPlayMode_ReturnsSuccess ()
        {
            const string operationName = "game.cheat.required";
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            op = operationName,
                            args = new { },
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = CreateNormalizer(CreatePlayModeOperation(operationName, UcliOperationPlayModeSupport.Required)).Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(
                result.Request!,
                0,
                new OperationExecutionContext(),
                allowPlayMode: true,
                CreateRegistry(CreatePlayModeOperation(operationName, UcliOperationPlayModeSupport.Required)));
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasLoweredOperations(IpcExecuteStepKind.Op, operationName, operationName);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeIsSpecified_RejectsUnknownRawOperationStep ()
        {
            const string operationName = "game.cheat.unknown";
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new[]
                    {
                        new
                        {
                            kind = "op",
                            op = operationName,
                            args = new { },
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = CreateNormalizer(Array.Empty<IUcliOperation>()).Normalize(request);

            AssertInvalidArgument(result, "/steps/0");
            Assert.That(result.Error!.Message, Is.EqualTo($"Operation '{operationName}' is not registered and cannot be used in Play Mode execution."));
        }

        [Test]
        [Category("Size.Small")]
        public void Compile_WhenOperationRequiresPlayModeWithoutAllowPlayMode_ReturnsInvalidArgument ()
        {
            const string operationName = "game.cheat.required";
            var sourceStep = ReadSingleSourceStep(
                CreateExecuteRequest(
                    UcliCommandIds.Plan.Name,
                    new
                    {
                        protocolVersion = IpcProtocol.CurrentVersion,
                        steps = new[]
                        {
                            new
                            {
                                kind = "op",
                                op = operationName,
                                args = new { },
                            },
                        },
                    }));
            var compiler = CreateCompiler(CreateRegistry(CreatePlayModeOperation(operationName, UcliOperationPlayModeSupport.Required)));

            var compiled = compiler.TryCompileExecutionStep(
                sourceStep,
                new OperationExecutionContext(),
                allowPlayMode: false,
                out _,
                out _,
                out _,
                out var error);

            Assert.That(compiled, Is.False);
            Assert.That(error.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(error.InstancePath, Is.EqualTo("/steps/0"));
            Assert.That(error.Message, Is.EqualTo($"Operation '{operationName}' requires --allowPlayMode."));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeDiffers_ProducesDifferentCanonicalDigestPayload ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = Array.Empty<object>(),
                });
            var playModeRequest = request with
            {
                AllowPlayMode = true,
            };

            var result = CreateNormalizer().Normalize(request);
            var playModeResult = CreateNormalizer().Normalize(playModeRequest);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(playModeResult.IsSuccess, Is.True);
            Assert.That(
                result.Request!.CanonicalDigestPayloadUtf8.Span.SequenceEqual(playModeResult.Request!.CanonicalDigestPayloadUtf8.Span),
                Is.False);
            var canonicalPayload = Encoding.UTF8.GetString(playModeResult.Request!.CanonicalDigestPayloadUtf8.ToArray());
            Assert.That(canonicalPayload, Does.Contain("\"allowPlayMode\":true"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeSceneCommitIsContext_ReturnsPersistenceForbiddenError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = "Assets/Scenes/Main.unity",
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(PlayModeErrorCodes.PlayModePersistenceForbidden));
            Assert.That(result.Error.InstancePath, Is.EqualTo("/steps/0"));
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
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: true);
            Assert.That(compiledOperations, Has.Count.EqualTo(1));
            Assert.That(
                compiledOperations[0].PersistenceReportingPolicy,
                Is.EqualTo(OperationPersistenceReportingPolicy.SuppressAll));
            Assert.That(compiledOperations[0].AllowExplicitPrefabAssetMutation, Is.False);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Edit,
                    IpcExecutePostReadCommit.None,
                    false,
                    IpcExecuteExpectedPostState.Unavailable,
                    expectedPlayModeMutation: true);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeSceneStepAppliesPrefabOverride_CompilesUnavailablePersistentPostReadSource ()
        {
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset();
            var prefabPath = scope.CreatePrefabAsset(nameof(ExecuteRequestNormalizerTests), "PrefabRoot");
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            _ = editableRoot.AddComponent<CompOperationTestComponent>();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);

            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAsset, Is.Not.Null);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset!);
            instance.name = "InstanceRoot";
            EditorSceneManager.SaveScene(scene, scenePath);
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "InstanceRoot",
                                component = componentTypeId,
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "set",
                                    values = new
                                    {
                                        integerValue = 42,
                                    },
                                },
                                new
                                {
                                    kind = "applyPrefabOverrides",
                                    targetAssetPath = prefabPath,
                                    propertyPaths = new[] { "integerValue" },
                                },
                            },
                            commit = "none",
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: true);
            Assert.That(compiledOperations.Select(static operation => operation.Op), Is.EqualTo(new[]
            {
                UcliPrimitiveOperationNames.CompSet,
                UcliPrimitiveOperationNames.PrefabApplyOverrides,
            }));
            Assert.That(compiledOperations[0].AllowExplicitPrefabAssetMutation, Is.True);
            Assert.That(compiledOperations[1].AllowExplicitPrefabAssetMutation, Is.False);
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Edit,
                    IpcExecutePostReadCommit.None,
                    true,
                    IpcExecuteExpectedPostState.Unavailable,
                    expectedPlayModeMutation: true);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeSceneStepCreatesPrefab_CompilesWithSceneReportingSuppressed ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestNormalizerTests));
            var prefabPath = scope.CreatePrefabPath(nameof(ExecuteRequestNormalizerTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "createPrefab",
                                    path = prefabPath,
                                },
                            },
                            commit = "none",
                        },
                    },
                }) with
                {
                    AllowPlayMode = true,
                };

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (compiledStep, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: true);
            Assert.That(compiledOperations, Has.Count.EqualTo(1));
            Assert.That(compiledOperations[0].Op, Is.EqualTo(UcliPrimitiveOperationNames.PrefabCreate));
            Assert.That(
                compiledOperations[0].PersistenceReportingPolicy,
                Is.EqualTo(OperationPersistenceReportingPolicy.SuppressScene));
            _ = new ExecuteRequestCompilerAssert(compiledStep, compiledOperations)
                .HasPostReadSourceStep(
                    IpcExecutePostReadSourceKind.Edit,
                    IpcExecutePostReadCommit.None,
                    true,
                    IpcExecuteExpectedPostState.Unavailable,
                    expectedPlayModeMutation: true);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenAllowPlayModeAssetCommitIsContext_CompilesTargetLimitedAssetSave ()
        {
            using var scope = new EditorTestScope();
            _ = scope.CreateScriptableAsset<AssetOperationTestAsset>(nameof(ExecuteRequestNormalizerTests), out var assetPath);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "asset",
                                path = assetPath,
                            },
                            select = new
                            {
                                kind = "self",
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

            var result = CreateNormalizer().Normalize(request);

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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (_, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: false);
            Assert.That(compiledOperations, Has.Count.EqualTo(1));
            Assert.That(compiledOperations[0].Op, Is.EqualTo(UcliPrimitiveOperationNames.PrefabRevertOverrides));
            Assert.That(
                compiledOperations[0].PersistenceReportingPolicy,
                Is.EqualTo(OperationPersistenceReportingPolicy.ReportAll));
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

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.True);
            var (_, compiledOperations) = CompileSingleStep(result.Request!, 0, scope.CreateExecutionContext(), allowPlayMode: true);
            Assert.That(compiledOperations, Has.Count.EqualTo(1));
            Assert.That(compiledOperations[0].Op, Is.EqualTo(UcliPrimitiveOperationNames.PrefabRevertOverrides));
            Assert.That(
                compiledOperations[0].PersistenceReportingPolicy,
                Is.EqualTo(OperationPersistenceReportingPolicy.SuppressAll));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenCommandIsValidate_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Validate.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = Array.Empty<object>(),
                });

            var result = CreateNormalizer().Normalize(request);

            AssertInvalidArgument(result);
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRequestJsonKeyOrderDiffers_ProducesStableCanonicalPayload ()
        {
            var requestA = CreateExecuteRequestFromJson(
                UcliCommandIds.Plan.Name,
                "{\"protocolVersion\":1,\"steps\":[{\"kind\":\"__OP_KIND__\",\"op\":\"__COMP_SET_OP__\",\"args\":{\"target\":{\"kind\":\"__SCENE_COMPONENT_KIND__\",\"scene\":\"Assets/Scenes/Main.unity\",\"hierarchyPath\":\"Root/Spawner\",\"componentType\":\"UnityEngine.BoxCollider, UnityEngine.PhysicsModule\"},\"sets\":[{\"path\":\"isTrigger\",\"value\":true}]}}]}"
                    .Replace("__COMP_SET_OP__", UcliPrimitiveOperationNames.CompSet, StringComparison.Ordinal)
                    .Replace(
                        "__OP_KIND__",
                        Vocabulary.GetText(IpcExecuteStepKind.Op),
                        StringComparison.Ordinal)
                    .Replace(
                        "__SCENE_COMPONENT_KIND__",
                        Vocabulary.GetText(UcliReferenceKind.SceneComponent),
                        StringComparison.Ordinal));
            var requestB = CreateExecuteRequestFromJson(
                UcliCommandIds.Plan.Name,
                "{\"steps\":[{\"args\":{\"sets\":[{\"value\":true,\"path\":\"isTrigger\"}],\"target\":{\"componentType\":\"UnityEngine.BoxCollider, UnityEngine.PhysicsModule\",\"hierarchyPath\":\"Root/Spawner\",\"scene\":\"Assets/Scenes/Main.unity\",\"kind\":\"__SCENE_COMPONENT_KIND__\"}},\"op\":\"__COMP_SET_OP__\",\"kind\":\"__OP_KIND__\"}],\"protocolVersion\":1}"
                    .Replace("__COMP_SET_OP__", UcliPrimitiveOperationNames.CompSet, StringComparison.Ordinal)
                    .Replace(
                        "__OP_KIND__",
                        Vocabulary.GetText(IpcExecuteStepKind.Op),
                        StringComparison.Ordinal)
                    .Replace(
                        "__SCENE_COMPONENT_KIND__",
                        Vocabulary.GetText(UcliReferenceKind.SceneComponent),
                        StringComparison.Ordinal));

            var normalizer = CreateNormalizer();
            var resultA = normalizer.Normalize(requestA);
            var resultB = normalizer.Normalize(requestB);

            Assert.That(resultA.IsSuccess, Is.True);
            Assert.That(resultB.IsSuccess, Is.True, resultB.Error?.ToString());
            Assert.That(
                Encoding.UTF8.GetString(resultA.Request!.CanonicalDigestPayloadUtf8.Span),
                Is.EqualTo(Encoding.UTF8.GetString(resultB.Request!.CanonicalDigestPayloadUtf8.Span)));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenRequestUsesDistinctExactInt64Values_ProducesDifferentDigestPayloads ()
        {
            var requestA = CreateExecuteRequestFromJson(
                UcliCommandIds.Plan.Name,
                "{\"protocolVersion\":1,\"steps\":[{\"kind\":\"op\",\"op\":\"__RESOLVE_OP__\",\"args\":{\"number\":9007199254740992}}]}"
                    .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));
            var requestB = CreateExecuteRequestFromJson(
                UcliCommandIds.Plan.Name,
                "{\"protocolVersion\":1,\"steps\":[{\"kind\":\"op\",\"op\":\"__RESOLVE_OP__\",\"args\":{\"number\":9007199254740993}}]}"
                    .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));

            var normalizer = CreateNormalizer();
            var resultA = normalizer.Normalize(requestA);
            var resultB = normalizer.Normalize(requestB);

            Assert.That(resultA.IsSuccess, Is.True);
            Assert.That(resultB.IsSuccess, Is.True);
            Assert.That(resultA.Request!.CanonicalDigestPayloadUtf8.Span.SequenceEqual(resultB.Request!.CanonicalDigestPayloadUtf8.Span), Is.False);
            Assert.That(
                Encoding.UTF8.GetString(resultA.Request!.CanonicalDigestPayloadUtf8.ToArray()),
                Does.Contain("\"number\":9007199254740992"));
            Assert.That(
                Encoding.UTF8.GetString(resultB.Request!.CanonicalDigestPayloadUtf8.ToArray()),
                Does.Contain("\"number\":9007199254740993"));
        }

        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenProtocolVersionMismatches_ReturnsProtocolVersionMismatchError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                new
                {
                    protocolVersion = 999,
                    steps = Array.Empty<object>(),
                });

            var result = CreateNormalizer().Normalize(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(IpcProtocolErrorCodes.ProtocolVersionMismatch));
            Assert.That(result.Error.InstancePath, Is.EqualTo("/protocolVersion"));
        }






        [Test]
        [Category("Size.Small")]
        public void Normalize_WhenSelectFromIsUsedOutsideSceneContext_ReturnsInvalidArgumentError ()
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "prefab",
                                path = "Assets/Prefabs/Enemy.prefab",
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                    {
                                        pathPrefix = "Root",
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

            var result = CreateNormalizer().Normalize(request);

            AssertInvalidArgument(result, "/steps/0");
        }


        private static IpcExecuteRequest CreatePrefabOverrideRevertRequest (
            string scenePath,
            bool allowPlayMode)
        {
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "Root",
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
            return CompileSingleStep(request, stepIndex, executionContext, allowPlayMode, CreateDefaultRegistry());
        }

        private static (NormalizedRequestStep Step, IReadOnlyList<NormalizedOperation> Operations) CompileSingleStep (
            NormalizedExecuteRequest request,
            int stepIndex,
            OperationExecutionContext executionContext,
            bool allowPlayMode,
            IPhaseOperationRegistry operationRegistry)
        {
            var compiler = CreateCompiler(operationRegistry);
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
            var compiler = CreateCompiler();
            var sourceStep = request.SourceSteps[stepIndex];
            Assert.That(
                compiler.TryCompileExecutionStep(sourceStep, executionContext, allowPlayMode: false, out _, out _, out _, out var error),
                Is.False);
            Assert.That(error, Is.Not.Null);
            return error;
        }

        private static ExecuteRequestNormalizer CreateNormalizer (params IUcliOperation[] operations)
        {
            return new ExecuteRequestNormalizer(
                operations.Length == 0
                    ? CreateDefaultRegistry()
                    : CreateRegistry(operations));
        }

        private static ExecuteRequestCompiler CreateCompiler ()
        {
            return CreateCompiler(CreateDefaultRegistry());
        }

        private static ExecuteRequestCompiler CreateCompiler (IPhaseOperationRegistry operationRegistry)
        {
            return new ExecuteRequestCompiler(operationRegistry);
        }

        private static IPhaseOperationRegistry CreateDefaultRegistry ()
        {
            return CreateRegistry(
                CreatePlayModeOperation(UcliPrimitiveOperationNames.CsEval, UcliOperationPlayModeSupport.Allowed),
                CreatePlayModeOperation(UcliPrimitiveOperationNames.CompSet, UcliOperationPlayModeSupport.Disallowed));
        }

        private static IPhaseOperationRegistry CreateRegistry (params IUcliOperation[] operations)
        {
            var registrations = new UcliOperationRegistration[operations.Length];
            for (var i = 0; i < operations.Length; i++)
            {
                registrations[i] = new UcliOperationRegistration(operations[i].Metadata, operations[i]);
            }

            return new InMemoryPhaseOperationRegistry(registrations);
        }

        private static IUcliOperation CreatePlayModeOperation (
            string operationName,
            UcliOperationPlayModeSupport playModeSupport)
        {
            return new TestRawOperation(UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: operationName,
                kind: UcliOperationKind.Mutation,
                description: $"{operationName} test operation.",
                assurance: new UcliOperationAssuranceContract(
                    sideEffects: new[] { UcliOperationSideEffect.RuntimeStateMutation },
                    touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                    planMode: UcliOperationPlanMode.ObservesLiveUnity,
                    planSemantics: "Validate Play Mode operation arguments without applying changes.",
                    callSemantics: "Apply a Play Mode runtime-state mutation.",
                    touchedContract: "Does not report persistent Unity resources.",
                    readPostconditionContract: "Persistent read surfaces are unchanged; runtime state may differ.",
                    failureSemantics: "Failure before invocation leaves runtime state unchanged.",
                    dangerousNotes: new[] { "Changes Play Mode runtime state and is not persisted." }),
                playModeSupport: playModeSupport));
        }

        private static IpcExecuteStepContract ReadSingleSourceStep (IpcExecuteRequest request)
        {
            Assert.That(
                IpcExecuteArgumentsContractReader.TryRead(
                    request.Arguments,
                    out var contract,
                    out var error),
                Is.True,
                error.ToString());
            return contract.Steps[0];
        }

        private static void AssertInvalidArgument (
            ExecuteRequestNormalizationResult result,
            string expectedInstancePath = null)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Error.InstancePath, Is.EqualTo(expectedInstancePath));
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

        private sealed class TestRawOperation : IUcliOperation
        {
            public TestRawOperation (UcliOperationMetadata metadata)
            {
                Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            }

            public UcliOperationMetadata Metadata { get; }

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

    }
}
