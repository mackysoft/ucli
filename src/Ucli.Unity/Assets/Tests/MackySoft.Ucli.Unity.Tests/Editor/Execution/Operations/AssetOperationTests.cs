using System;
using MackySoft.Ucli.Contracts;
using System.Collections;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class AssetOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Plan_WhenAliasIsSpecified_StoresTemporaryAssetAlias () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                },
                alias: "created");
            var context = scope.CreateExecutionContext();

            var result = await createOperation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertAssetSuccess(result, applied: false, changed: true, assetPath);
            Assert.That(result.Persisted, Is.False);
            Assert.That(context.TryGetTemporaryAliasState("created", out var aliasState), Is.True);
            Assert.That(aliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(aliasState.Resource.Path, Is.EqualTo(assetPath));
            Assert.That(aliasState.UnityObject, Is.TypeOf<AssetOperationTestAsset>());
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenArgumentsAreValid_CreatesAssetAndStoresAlias () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetCreateOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                },
                alias: "created");
            var context = scope.CreateExecutionContext();

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertAssetSuccess(result, applied: true, changed: true, assetPath);
            Assert.That(result.Persisted, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AssetOperationTestAsset>(assetPath), Is.Not.Null);
            Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.AssetSearch, null),
                (OperationReadInvalidationSurface.GuidPath, null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Call_WhenValueUsesCreatedAssetAlias_ResolvesPersistedAssetAlias () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            var setOperation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var createdAssetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var targetAsset = scope.CreateScriptableAsset<AssetOperationTestAsset>(nameof(AssetOperationTests), out var targetAssetPath);
            var context = scope.CreateExecutionContext();
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = createdAssetPath,
                },
                alias: "created");
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath = targetAssetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "assetReferenceValue",
                            value = new
                            {
                                @var = "created",
                            },
                        },
                    },
                });

            var createResult = await createOperation.CallAsync(createRequest, context, CancellationToken.None);
            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);

            AssertAssetSuccess(createResult, applied: true, changed: true, createdAssetPath);
            AssertAssetSuccess(setResult, applied: true, changed: true, targetAssetPath);
            var createdAsset = AssetDatabase.LoadAssetAtPath<AssetOperationTestAsset>(createdAssetPath);
            Assert.That(createdAsset, Is.Not.Null);
            Assert.That(targetAsset.AssetReferenceValue, Is.SameAs(createdAsset));
            Assert.That(context.TryGetTemporaryAliasState("created", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.UnityObject, Is.SameAs(createdAsset));
            AssertReadInvalidations(
                setResult,
                (OperationReadInvalidationSurface.AssetSearch, null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenTypeIsNotScriptableObject_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetCreateOperation();
            using var scope = new EditorTestScope();
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(Material)),
                    path = scope.CreateAssetPath(nameof(AssetOperationTests)),
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenSamePathIsAlreadyPlanned_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetCreateOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var context = scope.CreateExecutionContext();
            var firstRequest = CreateOperation(
                opId: "op-create-1",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                });
            var secondRequest = CreateOperation(
                opId: "op-create-2",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                });

            var firstResult = await operation.PlanAsync(firstRequest, context, CancellationToken.None);
            var secondResult = await operation.ValidateAsync(secondRequest, context, CancellationToken.None);

            AssertAssetSuccess(firstResult, applied: false, changed: true, assetPath);
            AssertInvalidArgument(secondResult, "op-create-2");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenSamePathIsAlreadyPlannedByDifferentExecutionKey_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetCreateOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var context = scope.CreateExecutionContext();
            var firstRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                },
                executionKey: "op-create#p0");
            var secondRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                },
                executionKey: "op-create#p1");

            var firstResult = await operation.PlanAsync(firstRequest, context, CancellationToken.None);
            var secondResult = await operation.ValidateAsync(secondRequest, context, CancellationToken.None);

            AssertAssetSuccess(firstResult, applied: false, changed: true, assetPath);
            AssertInvalidArgument(secondResult, "op-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Plan_WhenTargetUsesCreatedAlias_UpdatesTemporaryAssetState () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            var setOperation = new AssetSetOperation();
            var schemaOperation = new AssetSchemaOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var context = scope.CreateExecutionContext();
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                },
                alias: "created");
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        @var = "created",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                        new
                        {
                            path = "text",
                            value = "after",
                        },
                    },
                });
            var schemaRequest = CreateOperation(
                opId: "op-schema",
                opName: UcliPrimitiveOperationNames.AssetSchema,
                args: new
                {
                    target = new
                    {
                        @var = "created",
                    },
                });

            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var setResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var schemaResult = await schemaOperation.PlanAsync(schemaRequest, context, CancellationToken.None);

            AssertAssetSuccess(createResult, applied: false, changed: true, assetPath);
            AssertAssetSuccess(setResult, applied: false, changed: true, assetPath);
            AssertQuerySuccess(schemaResult, applied: false);
            Assert.That(context.TryGetTemporaryAliasState("created", out var aliasState), Is.True);
            Assert.That(aliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(aliasState.Resource.Path, Is.EqualTo(assetPath));
            Assert.That(aliasState.UnityObject, Is.TypeOf<AssetOperationTestAsset>());
            var temporaryAsset = (AssetOperationTestAsset)aliasState.UnityObject!;
            Assert.That(temporaryAsset.IntegerValue, Is.EqualTo(42));
            Assert.That(temporaryAsset.Text, Is.EqualTo("after"));

            var schema = schemaResult.Result!.Value;
            Assert.That(schema.GetProperty("schemaKey").GetString(), Does.StartWith("asset:"));
            Assert.That(schema.GetProperty("typeId").GetString(), Is.EqualTo(IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset))));
            Assert.That(schema.GetProperty("properties").ToString(), Does.Contain("\"path\":\"integerValue\""));
            Assert.That(schema.GetProperty("properties").ToString(), Does.Contain("\"path\":\"text\""));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Plan_WhenTargetUsesPlannedAssetPath_UpdatesTemporaryAssetState () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            var setOperation = new AssetSetOperation();
            var schemaOperation = new AssetSchemaOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var context = scope.CreateExecutionContext();
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                });
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 99,
                        },
                        new
                        {
                            path = "text",
                            value = "planned-path",
                        },
                    },
                });
            var schemaRequest = CreateOperation(
                opId: "op-schema",
                opName: UcliPrimitiveOperationNames.AssetSchema,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                });

            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var setResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var schemaResult = await schemaOperation.PlanAsync(schemaRequest, context, CancellationToken.None);

            AssertAssetSuccess(createResult, applied: false, changed: true, assetPath);
            AssertAssetSuccess(setResult, applied: false, changed: true, assetPath);
            AssertQuerySuccess(schemaResult, applied: false);
            Assert.That(context.TryGetPlannedAssetState(assetPath, out var plannedAssetState), Is.True);
            Assert.That(plannedAssetState.OwnerExecutionKey, Is.EqualTo("op-create"));
            Assert.That(plannedAssetState.UnityObject, Is.TypeOf<AssetOperationTestAsset>());
            var temporaryAsset = (AssetOperationTestAsset)plannedAssetState.UnityObject!;
            Assert.That(temporaryAsset.IntegerValue, Is.EqualTo(99));
            Assert.That(temporaryAsset.Text, Is.EqualTo("planned-path"));

            var schema = schemaResult.Result!.Value;
            Assert.That(schema.GetProperty("typeId").GetString(), Is.EqualTo(IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset))));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Plan_WhenValueUsesPlannedAssetPath_ResolvesPlannedAsset () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            var setOperation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var createdAssetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var targetAsset = scope.CreateScriptableAsset<AssetOperationTestAsset>(nameof(AssetOperationTests), out var targetAssetPath);
            var targetGlobalObjectId = UnityObjectReferenceResolver.CreateResolvedReference(targetAsset).GlobalObjectId;
            var context = scope.CreateExecutionContext();
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = createdAssetPath,
                });
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath = targetAssetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "assetReferenceValue",
                            value = new
                            {
                                assetPath = createdAssetPath,
                            },
                        },
                    },
                });

            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var setResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);

            AssertAssetSuccess(createResult, applied: false, changed: true, createdAssetPath);
            AssertAssetSuccess(setResult, applied: false, changed: true, targetAssetPath);
            Assert.That(context.TryGetPlannedAssetState(createdAssetPath, out var plannedAssetState), Is.True);
            Assert.That(context.TryGetAssetShadow(targetGlobalObjectId, out var shadowAsset, out var shadowAssetPath), Is.True);
            Assert.That(shadowAssetPath, Is.EqualTo(targetAssetPath));
            Assert.That(shadowAsset, Is.TypeOf<AssetOperationTestAsset>());
            Assert.That(((AssetOperationTestAsset)shadowAsset!).AssetReferenceValue, Is.SameAs(plannedAssetState.UnityObject));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Call_WhenTargetIsScriptableObjectAsset_UpdatesValueAndLeavesAssetDirty () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<AssetOperationTestAsset>(nameof(AssetOperationTests), out var assetPath);
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 64,
                        },
                        new
                        {
                            path = "text",
                            value = "updated",
                        },
                    },
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertAssetSuccess(result, applied: true, changed: true, assetPath);
            Assert.That(asset.IntegerValue, Is.EqualTo(64));
            Assert.That(asset.Text, Is.EqualTo("updated"));
            Assert.That(EditorUtility.IsDirty(asset), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Plan_WhenRawObjectReferenceValueSelectorMatchesPreviewOnlySceneObject_UsesRequestLocalPlanState () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<AssetOperationTestAsset>(nameof(AssetOperationTests), out var assetPath);
            var scenePath = scope.CreateScenePath(nameof(AssetOperationTests));
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            var previewOnly = new GameObject("PreviewOnly");
            SceneManager.MoveGameObjectToScene(previewOnly, temporaryScene);
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "objectReferenceValue",
                            value = new
                            {
                                scene = scenePath,
                                hierarchyPath = "PreviewOnly",
                            },
                        },
                    },
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertAssetSuccess(result, applied: false, changed: true, assetPath);
            var persistedAsset = AssetDatabase.LoadAssetAtPath<AssetOperationTestAsset>(assetPath);
            Assert.That(persistedAsset, Is.Not.Null);
            var sourceGlobalObjectId = UnityObjectReferenceResolver.CreateResolvedReference(persistedAsset).GlobalObjectId;
            Assert.That(context.TryGetAssetShadow(sourceGlobalObjectId, out var shadowAsset, out var shadowAssetPath), Is.True);
            Assert.That(shadowAssetPath, Is.EqualTo(assetPath));
            Assert.That(shadowAsset, Is.TypeOf<AssetOperationTestAsset>());
            Assert.That(((AssetOperationTestAsset)shadowAsset!).ObjectReferenceValue, Is.SameAs(previewOnly));
            Assert.That(asset.ObjectReferenceValue, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Call_WhenObjectReferenceValueSelectorMatchesPreviewOnlySceneObject_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<AssetOperationTestAsset>(nameof(AssetOperationTests), out var assetPath);
            var scenePath = scope.CreateScenePath(nameof(AssetOperationTests));
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var temporaryScene), Is.True);
            var previewOnly = new GameObject("PreviewOnly");
            SceneManager.MoveGameObjectToScene(previewOnly, temporaryScene);
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "objectReferenceValue",
                            value = new
                            {
                                scene = scenePath,
                                hierarchyPath = "PreviewOnly",
                            },
                        },
                    },
                });

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertInvalidArgument(result, "op-set");
            Assert.That(asset.ObjectReferenceValue, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Call_WhenLaterAssignmentFails_DoesNotPartiallyMutatePersistentAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var asset = scope.TrackUnityObject(ScriptableObject.CreateInstance<AssetOperationManagedReferenceTestAsset>());
            asset.SetNode(new AssetOperationManagedReferenceTestAsset.IntegerNode(7));
            AssetDatabase.CreateAsset(asset, assetPath);
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "node",
                            value = new
                            {
                                type = IndexTypeIdFormatter.Format(typeof(AssetOperationManagedReferenceTestAsset.TextNode)),
                                value = new
                                {
                                    text = "after",
                                },
                            },
                        },
                        new
                        {
                            path = "missing",
                            value = 1,
                        },
                    },
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-set");
            Assert.That(asset.Node, Is.TypeOf<AssetOperationManagedReferenceTestAsset.IntegerNode>());
            Assert.That(((AssetOperationManagedReferenceTestAsset.IntegerNode)asset.Node!).Number, Is.EqualTo(7));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Call_WhenTargetIsMaterialAsset_UpdatesMaterialSerializedValue () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests), ".mat");
            var material = scope.TrackUnityObject(new Material(ResolveMaterialShader()));
            AssetDatabase.CreateAsset(material, assetPath);
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "m_CustomRenderQueue",
                            value = 2450,
                        },
                    },
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertAssetSuccess(result, applied: true, changed: true, assetPath);
            Assert.That(material.renderQueue, Is.EqualTo(2450));
            Assert.That(EditorUtility.IsDirty(material), Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Validate_WhenTargetIsSubAsset_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSetOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests));
            var mainAsset = scope.TrackUnityObject(ScriptableObject.CreateInstance<AssetOperationTestAsset>());
            var subAsset = scope.TrackUnityObject(ScriptableObject.CreateInstance<AssetOperationTestAsset>());
            AssetDatabase.CreateAsset(mainAsset, assetPath);
            AssetDatabase.AddObjectToAsset(subAsset, assetPath);
            var context = scope.CreateExecutionContext();
            context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(subAsset));
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        @var = "target",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 10,
                        },
                    },
                });

            var result = await operation.ValidateAsync(requestOperation, context, CancellationToken.None);

            AssertInvalidArgument(result, "op-set");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Set_Validate_WhenTargetIsProjectSettingsAsset_ReturnsSuccess () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSetOperation();
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
                args: new
                {
                    target = new
                    {
                        projectAssetPath = "ProjectSettings/TagManager.asset",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "m_DefaultBehaviorMode",
                            value = 0,
                        },
                    },
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Touched, Is.Empty);
            Assert.That(result.Failure, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Schema_Plan_WhenTypeUsesMaterial_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSchemaOperation();
            var requestOperation = CreateOperation(
                opId: "op-schema",
                opName: UcliPrimitiveOperationNames.AssetSchema,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(Material)),
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-schema");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Schema_Plan_WhenTargetIsMaterial_ReturnsSchemaMetadata () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetSchemaOperation();
            using var scope = new EditorTestScope();
            var assetPath = scope.CreateAssetPath(nameof(AssetOperationTests), ".mat");
            var material = scope.TrackUnityObject(new Material(ResolveMaterialShader()));
            AssetDatabase.CreateAsset(material, assetPath);
            var requestOperation = CreateOperation(
                opId: "op-schema",
                opName: UcliPrimitiveOperationNames.AssetSchema,
                args: new
                {
                    target = new
                    {
                        assetPath,
                    },
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertQuerySuccess(result, applied: false);
            var schema = result.Result!.Value;
            Assert.That(schema.GetProperty("kind").GetString(), Is.EqualTo("asset"));
            Assert.That(schema.GetProperty("typeId").GetString(), Is.EqualTo(IndexTypeIdFormatter.Format(typeof(Material))));
        });

        private static Shader ResolveMaterialShader ()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("Material test shader could not be resolved.");
            }

            return shader;
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            string opName,
            object args,
            string? alias = null,
            string? executionKey = null)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: alias,
                Expect: null,
                InternalExecutionKey: executionKey);
        }

        private static void AssertInvalidArgument (
            OperationPhaseStepResult result,
            string expectedOperationId)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Failure.OpId, Is.EqualTo(expectedOperationId));
        }

        private static void AssertAssetSuccess (
            OperationPhaseStepResult result,
            bool applied,
            bool changed,
            string assetPath)
        {
            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(result.Touched[0].Path, Is.EqualTo(assetPath));
            Assert.That(result.Failure, Is.Null);
        }

        private static void AssertQuerySuccess (
            OperationPhaseStepResult result,
            bool applied)
        {
            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Touched, Is.Empty);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Failure, Is.Null);
        }

        private static void AssertReadInvalidations (
            OperationPhaseStepResult result,
            params (OperationReadInvalidationSurface Surface, string? ScenePath)[] expectedInvalidations)
        {
            Assert.That(result.ReadInvalidations.Count, Is.EqualTo(expectedInvalidations.Length));
            for (var i = 0; i < expectedInvalidations.Length; i++)
            {
                var expectedInvalidation = expectedInvalidations[i];
                Assert.That(result.ReadInvalidations[i].Surface, Is.EqualTo(expectedInvalidation.Surface));
                Assert.That(result.ReadInvalidations[i].ScenePath, Is.EqualTo(expectedInvalidation.ScenePath));
            }
        }

    }
}
