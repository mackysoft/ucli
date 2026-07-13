using System;
using MackySoft.Ucli.Contracts;
using System.Collections;
using System.IO;
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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PrefabOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Call_WhenSceneGameObjectIsValid_CreatesPrefabAndConnectsSourceObject () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabCreateOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var prefabPath = scope.CreatePrefabPath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-create",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    path = prefabPath,
                },
                alias: "created");

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: true, changed: true);
            Assert.That(result.Persisted, Is.True);
            AssertTouchSet(
                result,
                (OperationTouchKind.Scene, scenePath),
                (OperationTouchKind.Prefab, prefabPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(root), Is.True);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(root), Is.Not.Null);
            Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
            Assert.That(resolvedReference!.Value, Is.EqualTo(UnityObjectReferenceResolver.CreateGlobalObjectId(root).Value));
            AssertReadInvalidations(
                result,
                (OperationReadInvalidationSurface.AssetSearch, null),
                (OperationReadInvalidationSurface.GuidPath, null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_PlanAndCall_WhenPrefabWasCreatedEarlierInRequest_ResolvesPlannedPrefabLineage () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new PrefabCreateOperation();
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var prefabPath = scope.CreatePrefabPath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);

            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var target = new
            {
                scene = scenePath,
                hierarchyPath = "Root",
                componentType = componentTypeId,
            };
            var createRequest = CreateOperation(
                opId: "create-prefab",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var ensureRequest = CreateOperation(
                opId: "create-prefab",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    type = componentTypeId,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var setRequest = CreateOperation(
                opId: "apply-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var applyRequest = CreateOperation(
                opId: "apply-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabPath,
                    propertyPaths = new[] { "integerValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var ensurePlanResult = await ensureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);
            var createPlanResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var setPlanResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var applyPlanResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);
            var ensureCallResult = await ensureOperation.CallAsync(ensureRequest, context, CancellationToken.None);
            var createCallResult = await createOperation.CallAsync(createRequest, context, CancellationToken.None);
            var setCallResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var applyCallResult = await applyOperation.CallAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(ensurePlanResult, applied: false, changed: true);
            AssertSuccess(createPlanResult, applied: false, changed: true);
            AssertSuccess(setPlanResult, applied: false, changed: true);
            AssertSuccess(applyPlanResult, applied: false, changed: true);
            AssertTouchSet(applyPlanResult, (OperationTouchKind.Prefab, prefabPath));
            AssertSuccess(ensureCallResult, applied: true, changed: true);
            AssertSuccess(createCallResult, applied: true, changed: true);
            AssertSuccess(setCallResult, applied: true, changed: true);
            AssertSuccess(applyCallResult, applied: true, changed: true);
            AssertTouchSet(applyCallResult, (OperationTouchKind.Prefab, prefabPath));

            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            var loadedComponent = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(loadedComponent, Is.Not.Null);
            Assert.That(loadedComponent!.IntegerValue, Is.EqualTo(42));
            var component = root.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            Assert.That(component!.IntegerValue, Is.EqualTo(42));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Plan_WhenExistingComponentWasShadowedAfterPlannedPrefabCreation_ResolvesPlannedPrefabLineage () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new PrefabCreateOperation();
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var prefabPath = scope.CreatePrefabPath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            _ = root.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);

            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var target = new
            {
                scene = scenePath,
                hierarchyPath = "Root",
                componentType = componentTypeId,
            };
            var createRequest = CreateOperation(
                opId: "create-prefab",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var setRequest = CreateOperation(
                opId: "apply-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var applyRequest = CreateOperation(
                opId: "apply-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabPath,
                    propertyPaths = new[] { "integerValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var createPlanResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var setPlanResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var applyPlanResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(createPlanResult, applied: false, changed: true);
            AssertSuccess(setPlanResult, applied: false, changed: true);
            AssertSuccess(applyPlanResult, applied: false, changed: true);
            AssertTouchSet(applyPlanResult, (OperationTouchKind.Prefab, prefabPath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Plan_WhenLaterSetRestoresPreRequestValue_RejectsPropertyPath () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabInstance = CreateSavedPrefabInstanceWithTestComponent(scope);

            var target = new
            {
                scene = prefabInstance.ScenePath,
                hierarchyPath = "InstanceRoot",
                componentType = prefabInstance.ComponentTypeId,
            };
            var changeSetRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var restoreSetRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 1,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var changeSetResult = await setOperation.PlanAsync(changeSetRequest, context, CancellationToken.None);
            var restoreSetResult = await setOperation.PlanAsync(restoreSetRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(changeSetResult, applied: false, changed: true);
            AssertSuccess(restoreSetResult, applied: false, changed: true);
            AssertInvalidArgument(applyResult, "edit-step");
            Assert.That(applyResult.Failure!.Message, Does.Contain("pre-request value"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Plan_WhenExistingComponentShadowReceivesSecondProperty_TracksSecondProperty () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabInstance = CreateSavedPrefabInstanceWithTestComponent(scope);

            var target = new
            {
                scene = prefabInstance.ScenePath,
                hierarchyPath = "InstanceRoot",
                componentType = prefabInstance.ComponentTypeId,
            };
            var firstSetRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var secondSetRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "floatValue",
                            value = 3.5f,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "floatValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var firstSetResult = await setOperation.PlanAsync(firstSetRequest, context, CancellationToken.None);
            var secondSetResult = await setOperation.PlanAsync(secondSetRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(firstSetResult, applied: false, changed: true);
            AssertSuccess(secondSetResult, applied: false, changed: true);
            AssertSuccess(applyResult, applied: false, changed: true);
            AssertTouchSet(applyResult, (OperationTouchKind.Prefab, prefabInstance.PrefabPath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Plan_WhenAliasedComponentShadowReceivesSecondProperty_TracksSecondProperty () => UniTask.ToCoroutine(async () =>
        {
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabInstance = CreateSavedPrefabInstanceWithTestComponent(scope);

            var aliasTarget = new
            {
                @var = "component",
            };
            var ensureRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        scene = prefabInstance.ScenePath,
                        hierarchyPath = "InstanceRoot",
                    },
                    type = prefabInstance.ComponentTypeId,
                },
                alias: "component",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var firstSetRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = aliasTarget,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var secondSetRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = aliasTarget,
                    sets = new object[]
                    {
                        new
                        {
                            path = "floatValue",
                            value = 3.5f,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target = aliasTarget,
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "floatValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var ensureResult = await ensureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);
            var firstSetResult = await setOperation.PlanAsync(firstSetRequest, context, CancellationToken.None);
            var secondSetResult = await setOperation.PlanAsync(secondSetRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(ensureResult, applied: false, changed: false);
            AssertSuccess(firstSetResult, applied: false, changed: true);
            AssertSuccess(secondSetResult, applied: false, changed: true);
            AssertSuccess(applyResult, applied: false, changed: true);
            AssertTouchSet(applyResult, (OperationTouchKind.Prefab, prefabInstance.PrefabPath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Plan_WhenSetAndApplyUseEquivalentGlobalObjectIdSpellings_CorrelatesCanonicalTarget () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabInstance = CreateSavedPrefabInstanceWithTestComponent(scope);
            var canonicalGlobalObjectId = UnityObjectReferenceResolver.CreateGlobalObjectId(prefabInstance.Component).Value;
            var nonCanonicalGlobalObjectId = GlobalObjectIdTestValues.CreateNonCanonicalIdentifierTypeText(canonicalGlobalObjectId);
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        globalObjectId = nonCanonicalGlobalObjectId,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target = new
                    {
                        globalObjectId = canonicalGlobalObjectId,
                    },
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var setResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(setResult, applied: false, changed: true);
            AssertSuccess(applyResult, applied: false, changed: true);
            AssertTouchSet(applyResult, (OperationTouchKind.Prefab, prefabInstance.PrefabPath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Plan_WhenObjectWasReparentedIntoPlannedPrefabCreation_RejectsTarget () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new PrefabCreateOperation();
            var reparentOperation = new GoReparentOperation();
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var prefabPath = scope.CreatePrefabPath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("Root");
            var external = new GameObject("External");
            _ = external.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);

            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var target = new
            {
                scene = scenePath,
                hierarchyPath = "Root/External",
                componentType = componentTypeId,
            };
            var createRequest = CreateOperation(
                opId: "create-prefab",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var reparentRequest = CreateOperation(
                opId: "reparent",
                opName: UcliPrimitiveOperationNames.GoReparent,
                args: new
                {
                    target = new
                    {
                        scene = scenePath,
                        hierarchyPath = "External",
                    },
                    parent = new
                    {
                        scene = scenePath,
                        hierarchyPath = "Root",
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "floatValue",
                            value = 3.5f,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabPath,
                    propertyPaths = new[] { "floatValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var reparentResult = await reparentOperation.PlanAsync(reparentRequest, context, CancellationToken.None);
            var setResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(createResult, applied: false, changed: true);
            AssertSuccess(reparentResult, applied: false, changed: true);
            AssertSuccess(setResult, applied: false, changed: true);
            AssertInvalidArgument(applyResult, "edit-step");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Create_Validate_WhenTargetIsMissing_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-prefab-create",
                opName: UcliPrimitiveOperationNames.PrefabCreate,
                args: new
                {
                    path = "Assets/MissingTarget.prefab",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-create");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_WhenDirtyLoadedSceneExists_ReturnsInvalidArgumentBeforeOpening () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("DirtyRoot");
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty loaded scene blocks opening prefab"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenDirtyLoadedSceneExists_ReturnsInvalidArgumentBeforePlanning () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var dirtyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("DirtyRoot");
            EditorSceneManager.MarkSceneDirty(dirtyScene);
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty loaded scene blocks opening prefab"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenAnotherDirtyPrefabStageExists_ReturnsInvalidArgumentBeforePlanning () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var dirtyPrefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "DirtyPrefabRoot");
            var targetPrefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "TargetPrefabRoot");
            var dirtyPrefabStage = PrefabStageUtility.OpenPrefab(dirtyPrefabPath);
            var child = new GameObject("DirtyChild");
            child.transform.SetParent(dirtyPrefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(dirtyPrefabStage.scene);
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = targetPrefabPath,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-prefab-open");
            Assert.That(result.Failure!.Message, Does.Contain("Dirty prefab stage blocks opening prefab"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenAliasIsSpecified_StoresTemporaryPrefabRootAlias () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertTouchSet(result, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryAliasState("root", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<GameObject>());
            Assert.That(
                ((GameObject)temporaryAliasState.UnityObject!).name,
                Is.EqualTo(Path.GetFileNameWithoutExtension(prefabPath)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenRawPrefabPathIsValid_DoesNotTrackTemporaryPrefabRoot () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root");

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertTouchSet(result, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out _), Is.False);
            Assert.That(context.TryGetTemporaryAliasState("root", out _), Is.False);
            Assert.That(PrefabStageUtility.GetCurrentPrefabStage(), Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenFollowedByGoCreateAndCompEnsureAndSet_AllowsTemporaryAliasChain () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var goCreateOperation = new GoCreateOperation();
            var compEnsureOperation = new CompEnsureOperation();
            var compSetOperation = new CompSetOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var createRequest = CreateOperation(
                opId: "op-go-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "Child",
                    parent = new
                    {
                        @var = "root",
                    },
                },
                alias: "child",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        @var = "child",
                    },
                    type = componentTypeId,
                },
                alias: "childComp",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var setRequest = CreateOperation(
                opId: "op-comp-set",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        @var = "childComp",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 11,
                        },
                    },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            var createResult = await goCreateOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var ensureResult = await compEnsureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);
            var setResult = await compSetOperation.PlanAsync(setRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);
            AssertSuccess(createResult, applied: false, changed: true);
            AssertSuccess(ensureResult, applied: false, changed: true);
            AssertSuccess(setResult, applied: false, changed: true);
            Assert.That(context.TryGetTemporaryAliasState("childComp", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<CompOperationTestComponent>());
            Assert.That(((CompOperationTestComponent)temporaryAliasState.UnityObject!).IntegerValue, Is.EqualTo(11));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenOpenedPrefabStageIsDirty_TracksTemporaryPrefabContentsSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot", "Child");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            prefabStage!.prefabContentsRoot.transform.GetChild(0).name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertTouchSet(result, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var prefabContentsRoot), Is.True);
            Assert.That(prefabContentsRoot, Is.Not.Null);
            Assert.That(prefabContentsRoot, Is.Not.SameAs(prefabStage.prefabContentsRoot));
            Assert.That(prefabContentsRoot!.transform.GetChild(0).name, Is.EqualTo("Renamed"));
            Assert.That(context.TryGetTemporaryAliasState("root", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.SameAs(prefabContentsRoot));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenOpenedPrefabStageIsDirty_RebindsCrossRootObjectReferencesInsideTemporaryPrefabSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var operation = new PrefabOpenOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            var sourceA = new GameObject("A");
            sourceA.transform.SetParent(editableRoot.transform, worldPositionStays: false);
            var sourceComponent = sourceA.AddComponent<CompOperationTestComponent>();
            var sourceB = new GameObject("B");
            sourceB.transform.SetParent(editableRoot.transform, worldPositionStays: false);
            var serializedObject = new SerializedObject(sourceComponent);
            serializedObject.FindProperty("objectReferenceValue").objectReferenceValue = sourceB;
            serializedObject.FindProperty("componentReferenceValue").objectReferenceValue = sourceB.transform;
            serializedObject.FindProperty("exposedObjectReferenceValue.defaultValue").objectReferenceValue = sourceB;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            var stageRoot = prefabStage!.prefabContentsRoot;
            var stageB = stageRoot.transform.Find("B");
            Assert.That(stageB, Is.Not.Null);
            stageB!.name = "RenamedB";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var context = scope.CreateExecutionContext();
            var requestOperation = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var prefabContentsRoot), Is.True);
            Assert.That(prefabContentsRoot, Is.Not.Null);
            var previewA = prefabContentsRoot!.transform.Find("A");
            var previewB = prefabContentsRoot.transform.Find("RenamedB");
            Assert.That(previewA, Is.Not.Null);
            Assert.That(previewB, Is.Not.Null);
            var previewComponent = previewA!.GetComponent<CompOperationTestComponent>();
            Assert.That(previewComponent, Is.Not.Null);
            Assert.That(previewComponent!.ObjectReferenceValue, Is.SameAs(previewB!.gameObject));
            Assert.That(previewComponent.ObjectReferenceValue, Is.Not.SameAs(stageB.gameObject));
            Assert.That(previewComponent.ComponentReferenceValue, Is.SameAs(previewB.transform));
            Assert.That(previewComponent.ComponentReferenceValue, Is.Not.SameAs(stageB.transform));
            Assert.That(previewComponent.ExposedObjectReferenceValue, Is.SameAs(previewB.gameObject));
            Assert.That(previewComponent.ExposedObjectReferenceValue, Is.Not.SameAs(stageB.gameObject));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Plan_WhenCompEnsureTargetsPrefabSelector_UsesPrefabOwnerResource () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var ensureOperation = new CompEnsureOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        prefab = prefabPath,
                        hierarchyPath = Path.GetFileNameWithoutExtension(prefabPath),
                    },
                    type = componentTypeId,
                },
                alias: "component",
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.PlanAsync(ensureRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: false, changed: false);
            AssertSuccess(ensureResult, applied: false, changed: true);
            AssertTouchSet(ensureResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.TryGetTemporaryAliasState("component", out var temporaryAliasState), Is.True);
            Assert.That(temporaryAliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Prefab));
            Assert.That(temporaryAliasState.Resource.Path, Is.EqualTo(prefabPath));
            Assert.That(temporaryAliasState.UnityObject, Is.TypeOf<CompOperationTestComponent>());
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_ThenCompEnsureSet_ThenSave_Call_PersistsComponentChanges () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root");
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        @var = "root",
                    },
                    type = componentTypeId,
                },
                alias: "component");
            var setRequest = CreateOperation(
                opId: "op-comp-set",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        @var = "component",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                });
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.CallAsync(ensureRequest, context, CancellationToken.None);
            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: true, changed: false);
            AssertSuccess(ensureResult, applied: true, changed: true);
            AssertSuccess(setResult, applied: true, changed: true);
            AssertSuccess(saveResult, applied: true, changed: true);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            var openedPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(openedPrefabStage, Is.Not.Null);
            Assert.That(openedPrefabStage!.prefabContentsRoot.scene.isDirty, Is.False);

            scope.CloseCurrentPrefabStageIfOpen();
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            var component = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            Assert.That(component!.IntegerValue, Is.EqualTo(42));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Open_Call_ThenGoCreateAndCompEnsureSet_ThenSave_Call_PersistsChildChanges () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var goCreateOperation = new GoCreateOperation();
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var componentTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                alias: "root");
            var createRequest = CreateOperation(
                opId: "op-go-create",
                opName: UcliPrimitiveOperationNames.GoCreate,
                args: new
                {
                    name = "Child",
                    parent = new
                    {
                        @var = "root",
                    },
                },
                alias: "child");
            var ensureRequest = CreateOperation(
                opId: "op-comp-ensure",
                opName: UcliPrimitiveOperationNames.CompEnsure,
                args: new
                {
                    target = new
                    {
                        @var = "child",
                    },
                    type = componentTypeId,
                },
                alias: "childComp");
            var setRequest = CreateOperation(
                opId: "op-comp-set",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        @var = "childComp",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 7,
                        },
                    },
                });
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            var createResult = await goCreateOperation.CallAsync(createRequest, context, CancellationToken.None);
            var ensureResult = await ensureOperation.CallAsync(ensureRequest, context, CancellationToken.None);
            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(openResult, applied: true, changed: false);
            AssertSuccess(createResult, applied: true, changed: true);
            AssertSuccess(ensureResult, applied: true, changed: true);
            AssertSuccess(setResult, applied: true, changed: true);
            AssertSuccess(saveResult, applied: true, changed: true);

            scope.CloseCurrentPrefabStageIfOpen();
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            var child = loadedPrefabContentsRoot.transform.Find("Child");
            Assert.That(child, Is.Not.Null);
            var component = child!.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            Assert.That(component!.IntegerValue, Is.EqualTo(7));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOpenedPrefabStageIsDirtyWithoutRequestChange_SavesOpenedPrefabStage () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            var child = new GameObject("Child");
            child.transform.SetParent(prefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.prefabContentsRoot.scene);
            Assert.That(prefabStage.prefabContentsRoot.scene.isDirty, Is.True);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            Assert.That(saveResult.Persisted, Is.True);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(prefabStage.prefabContentsRoot.scene.isDirty, Is.False);
            AssertReadInvalidations(
                saveResult,
                (OperationReadInvalidationSurface.AssetSearch, null));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenOnlyTemporaryPrefabPreviewExistsWithoutPlannedLiveOpen_Succeeds () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.PlanAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: false, changed: false);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOnlyTemporaryPrefabPreviewHasRequestAttributedChange_SavesTemporaryPrefabContents () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            var child = new GameObject("SavedChild");
            child.transform.SetParent(temporaryRoot!.transform, worldPositionStays: false);
            context.MarkRequestAttributedChange(new OperationResource(OperationTouchKind.Prefab, prefabPath));
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            Assert.That(saveResult.Persisted, Is.True);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(context.HasRequestAttributedChange(new OperationResource(OperationTouchKind.Prefab, prefabPath)), Is.False);

            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(loadedPrefabContentsRoot.transform.Find("SavedChild"), Is.Not.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenOpenedPrefabStageAndTemporaryPreviewExist_SavesOpenedPrefabStage () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            var temporaryChild = new GameObject("TemporaryChild");
            temporaryChild.transform.SetParent(temporaryRoot!.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(temporaryRoot.scene);

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = new GameObject("StageChild");
            stageChild.transform.SetParent(prefabStage!.prefabContentsRoot.transform, worldPositionStays: false);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(saveResult, applied: true, changed: true);
            Assert.That(saveResult.Persisted, Is.True);
            AssertTouchSet(saveResult, (OperationTouchKind.Prefab, prefabPath));
            Assert.That(prefabStage.scene.isDirty, Is.False);
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(loadedPrefabContentsRoot.transform.Find("StageChild"), Is.Not.Null);
            Assert.That(loadedPrefabContentsRoot.transform.Find("TemporaryChild"), Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RevertOverrides_Call_WhenPlanPreviewExists_RevertsLivePrefabInstance () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var revertOperation = new PrefabRevertOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset();
            var prefabInstance = CreateSavedPrefabInstanceWithTestComponent(scope);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            var target = new
            {
                scene = prefabInstance.ScenePath,
                hierarchyPath = "InstanceRoot",
                componentType = prefabInstance.ComponentTypeId,
            };
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                });
            var revertRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabRevertOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                });

            var planSetResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var callSetResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var revertResult = await revertOperation.CallAsync(revertRequest, context, CancellationToken.None);

            AssertSuccess(planSetResult, applied: false, changed: true);
            AssertSuccess(callSetResult, applied: true, changed: true);
            AssertSuccess(revertResult, applied: true, changed: true);
            AssertTouchSet(revertResult, (OperationTouchKind.Scene, prefabInstance.ScenePath));
            AssertReadInvalidations(
                revertResult,
                (OperationReadInvalidationSurface.SceneTreeLite, prefabInstance.ScenePath.Replace('\\', '/')));
            Assert.That(prefabInstance.Component.IntegerValue, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_PlanThenCall_WhenTargetUsesTemporaryOnlyAlias_AppliesLiveComponentOverride () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset();
            var prefabInstance = CreateSavedPrefabInstanceWithTestComponent(scope);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            context.SetTemporaryAlias(
                "component",
                prefabInstance.Component,
                new OperationResource(OperationTouchKind.Scene, prefabInstance.ScenePath));
            Assert.That(context.AliasStore.TryGet("component", out _), Is.False);

            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target = new
                    {
                        scene = prefabInstance.ScenePath,
                        hierarchyPath = "InstanceRoot",
                        componentType = prefabInstance.ComponentTypeId,
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                });
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target = new
                    {
                        @var = "component",
                    },
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);

            var setPlanResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var applyPlanResult = await applyOperation.PlanAsync(applyRequest, context, CancellationToken.None);
            var setCallResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var applyCallResult = await applyOperation.CallAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(setPlanResult, applied: false, changed: true);
            AssertSuccess(applyPlanResult, applied: false, changed: true);
            AssertSuccess(setCallResult, applied: true, changed: true);
            AssertSuccess(applyCallResult, applied: true, changed: true);
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(prefabInstance.PrefabPath);
            var loadedComponent = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(loadedComponent, Is.Not.Null);
            Assert.That(loadedComponent!.IntegerValue, Is.EqualTo(42));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Call_WhenPlayModeLiveSceneFallbackIsRecorded_CopiesLiveValueToExplicitPrefabAsset () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var fixture = CreateDisconnectedSceneObjectWithMatchingPrefab(scope);
            var context = scope.CreateExecutionContext();
            context.TrackPlannedPrefabCreation(
                fixture.Component.gameObject,
                new OperationResource(OperationTouchKind.Scene, fixture.ScenePath),
                fixture.PrefabPath);
            Assert.That(context.TryEnsureSceneExecutionSession(fixture.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var target = new
            {
                scene = fixture.ScenePath,
                hierarchyPath = "PrefabRoot",
                componentType = fixture.ComponentTypeId,
            };
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                suppressPersistenceReporting: true,
                allowExplicitPrefabAssetMutation: true);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = fixture.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                });

            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.CallAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(setResult, applied: true, changed: true);
            AssertSuccess(applyResult, applied: true, changed: true);
            Assert.That(applyResult.Persisted, Is.True);
            AssertTouchSet(applyResult, (OperationTouchKind.Prefab, fixture.PrefabPath));
            AssertReadInvalidations(
                applyResult,
                (OperationReadInvalidationSurface.AssetSearch, null),
                (OperationReadInvalidationSurface.GuidPath, null));
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(fixture.PrefabPath);
            var loadedComponent = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(loadedComponent, Is.Not.Null);
            Assert.That(loadedComponent!.IntegerValue, Is.EqualTo(42));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RevertOverrides_Call_WhenPlayModeLiveSceneFallbackIsRecorded_CopiesExplicitPrefabAssetValueToLiveTarget () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var revertOperation = new PrefabRevertOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var fixture = CreateDisconnectedSceneObjectWithMatchingPrefab(scope);
            var context = scope.CreateExecutionContext();
            context.TrackPlannedPrefabCreation(
                fixture.Component.gameObject,
                new OperationResource(OperationTouchKind.Scene, fixture.ScenePath),
                fixture.PrefabPath);
            Assert.That(context.TryEnsureSceneExecutionSession(fixture.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var target = new
            {
                scene = fixture.ScenePath,
                hierarchyPath = "PrefabRoot",
                componentType = fixture.ComponentTypeId,
            };
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                suppressPersistenceReporting: true,
                allowExplicitPrefabAssetMutation: true);
            var revertRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabRevertOverrides,
                args: new
                {
                    target,
                    targetAssetPath = fixture.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                },
                suppressPersistenceReporting: true);

            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var revertResult = await revertOperation.CallAsync(revertRequest, context, CancellationToken.None);

            AssertSuccess(setResult, applied: true, changed: true);
            AssertSuccess(revertResult, applied: true, changed: true);
            Assert.That(fixture.Component.IntegerValue, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator RevertOverrides_Call_WhenExplicitFallbackAssetValueDiffersBeforeRequest_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var revertOperation = new PrefabRevertOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var fixture = CreateDisconnectedSceneObjectWithMatchingPrefab(scope);
            SetPrefabComponentIntegerValue(scope, fixture.PrefabPath, 7);
            var context = scope.CreateExecutionContext();
            context.TrackPlannedPrefabCreation(
                fixture.Component.gameObject,
                new OperationResource(OperationTouchKind.Scene, fixture.ScenePath),
                fixture.PrefabPath);
            Assert.That(context.TryEnsureSceneExecutionSession(fixture.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var target = new
            {
                scene = fixture.ScenePath,
                hierarchyPath = "PrefabRoot",
                componentType = fixture.ComponentTypeId,
            };
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                suppressPersistenceReporting: true,
                allowExplicitPrefabAssetMutation: true);
            var revertRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabRevertOverrides,
                args: new
                {
                    target,
                    targetAssetPath = fixture.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                },
                suppressPersistenceReporting: true);

            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var revertResult = await revertOperation.CallAsync(revertRequest, context, CancellationToken.None);

            AssertSuccess(setResult, applied: true, changed: true);
            AssertInvalidArgument(revertResult, "edit-step");
            Assert.That(fixture.Component.IntegerValue, Is.EqualTo(42));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Call_WhenExplicitFallbackHasNoPrefabLineage_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var fixture = CreateDisconnectedSceneObjectWithMatchingPrefab(scope);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(fixture.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var target = new
            {
                scene = fixture.ScenePath,
                hierarchyPath = "PrefabRoot",
                componentType = fixture.ComponentTypeId,
            };
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                },
                suppressPersistenceReporting: true,
                allowExplicitPrefabAssetMutation: true);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = fixture.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                });

            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.CallAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(setResult, applied: true, changed: true);
            AssertInvalidArgument(applyResult, "edit-step");
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(fixture.PrefabPath);
            var loadedComponent = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(loadedComponent, Is.Not.Null);
            Assert.That(loadedComponent!.IntegerValue, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ApplyOverrides_Call_WhenExplicitFallbackValueContainsSceneReference_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset()
                .EnablePrefabStageCleanup();
            var fixture = CreateDisconnectedSceneObjectWithMatchingPrefab(scope);
            var sceneReference = new GameObject("SceneOnlyReference");
            SceneManager.MoveGameObjectToScene(sceneReference, fixture.Component.gameObject.scene);
            var context = scope.CreateExecutionContext();
            context.TrackPlannedPrefabCreation(
                fixture.Component.gameObject,
                new OperationResource(OperationTouchKind.Scene, fixture.ScenePath),
                fixture.PrefabPath);
            Assert.That(context.TryEnsureSceneExecutionSession(fixture.ScenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var target = new
            {
                scene = fixture.ScenePath,
                hierarchyPath = "PrefabRoot",
                componentType = fixture.ComponentTypeId,
            };
            var setRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "objectReferenceValue",
                            value = new
                            {
                                scene = fixture.ScenePath,
                                hierarchyPath = "SceneOnlyReference",
                            },
                        },
                    },
                },
                suppressPersistenceReporting: true,
                allowExplicitPrefabAssetMutation: true);
            var applyRequest = CreateOperation(
                opId: "edit-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = fixture.PrefabPath,
                    propertyPaths = new[] { "objectReferenceValue" },
                });

            var setResult = await setOperation.CallAsync(setRequest, context, CancellationToken.None);
            var applyResult = await applyOperation.CallAsync(applyRequest, context, CancellationToken.None);

            AssertSuccess(setResult, applied: true, changed: true);
            AssertInvalidArgument(applyResult, "edit-step");
            var loadedPrefabContentsRoot = scope.LoadPrefabContents(fixture.PrefabPath);
            var loadedComponent = loadedPrefabContentsRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(loadedComponent, Is.Not.Null);
            Assert.That(loadedComponent!.ObjectReferenceValue, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrefabOverrideActions_Plan_WhenDirtyScenePreviewMirrorsPrefabInstance_UseSourceTrackingKey () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new CompSetOperation();
            var applyOperation = new PrefabApplyOverridesOperation();
            var revertOperation = new PrefabRevertOverridesOperation();
            using var scope = new EditorTestScope()
                .EnableEditorSceneReset();
            var prefabInstance = CreateDirtyPrefabPreviewWithTestComponent(scope);

            var target = new
            {
                scene = prefabInstance.ScenePath,
                hierarchyPath = "InstanceRoot",
                componentType = prefabInstance.ComponentTypeId,
            };
            var applySetRequest = CreateOperation(
                opId: "apply-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "floatValue",
                            value = 2.5f,
                        },
                    },
                });
            var applyRequest = CreateOperation(
                opId: "apply-step",
                opName: UcliPrimitiveOperationNames.PrefabApplyOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "floatValue" },
                });
            var revertSetRequest = CreateOperation(
                opId: "revert-step",
                opName: UcliPrimitiveOperationNames.CompSet,
                args: new
                {
                    target,
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                    },
                });
            var revertRequest = CreateOperation(
                opId: "revert-step",
                opName: UcliPrimitiveOperationNames.PrefabRevertOverrides,
                args: new
                {
                    target,
                    targetAssetPath = prefabInstance.PrefabPath,
                    propertyPaths = new[] { "integerValue" },
                });

            using var applyContext = scope.CreateExecutionContext();
            Assert.That(applyContext.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var applyEnsureErrorMessage), Is.True, applyEnsureErrorMessage);
            var applySetPlanResult = await setOperation.PlanAsync(applySetRequest, applyContext, CancellationToken.None);
            var applyPlanResult = await applyOperation.PlanAsync(applyRequest, applyContext, CancellationToken.None);
            using var revertContext = scope.CreateExecutionContext();
            Assert.That(revertContext.TryEnsureSceneExecutionSession(prefabInstance.ScenePath, out var revertEnsureErrorMessage), Is.True, revertEnsureErrorMessage);
            var revertSetPlanResult = await setOperation.PlanAsync(revertSetRequest, revertContext, CancellationToken.None);
            var revertPlanResult = await revertOperation.PlanAsync(revertRequest, revertContext, CancellationToken.None);

            AssertSuccess(applySetPlanResult, applied: false, changed: true);
            AssertSuccess(applyPlanResult, applied: false, changed: true);
            AssertTouchSet(applyPlanResult, (OperationTouchKind.Prefab, prefabInstance.PrefabPath));
            AssertSuccess(revertSetPlanResult, applied: false, changed: true);
            AssertSuccess(revertPlanResult, applied: false, changed: true);
            AssertTouchSet(revertPlanResult, (OperationTouchKind.Scene, prefabInstance.ScenePath));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Plan_WhenPrefabOpenWasPlannedForClosedPrefab_Succeeds () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var openRequest = CreateOperation(
                opId: "op-prefab-open",
                opName: UcliPrimitiveOperationNames.PrefabOpen,
                args: new
                {
                    path = prefabPath,
                },
                sourceKind: NormalizedOperation.SourceStepKind.Edit);
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var openPlanResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            var savePlanResult = await saveOperation.PlanAsync(saveRequest, context, CancellationToken.None);

            AssertSuccess(openPlanResult, applied: false, changed: false);
            AssertSuccess(savePlanResult, applied: false, changed: false);
            Assert.That(savePlanResult.Persisted, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Save_Call_WhenPrefabIsNotOpened_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var saveOperation = new PrefabSaveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var saveRequest = CreateOperation(
                opId: "op-prefab-save",
                opName: UcliPrimitiveOperationNames.PrefabSave,
                args: new
                {
                    path = prefabPath,
                });

            var saveResult = await saveOperation.CallAsync(saveRequest, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(saveResult, "op-prefab-save");
        });

        /// <summary> Creates a saved scene prefab instance backed by a prefab asset that contains <see cref="CompOperationTestComponent" />. </summary>
        /// <param name="scope"> The test scope that owns created assets and loaded prefab contents. </param>
        /// <returns> The prefab path, scene path, instance component, and indexed component type identifier. </returns>
        private static (
            string PrefabPath,
            string ScenePath,
            CompOperationTestComponent Component,
            string ComponentTypeId) CreateSavedPrefabInstanceWithTestComponent (EditorTestScope scope)
        {
            var prefabPath = CreatePrefabAssetWithTestComponent(scope);
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var component = InstantiatePrefabInstanceWithTestComponent(prefabPath);
            EditorSceneManager.SaveScene(scene, scenePath);

            return (
                prefabPath,
                scenePath,
                component,
                IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)));
        }

        /// <summary> Creates a dirty scene prefab instance backed by a prefab asset that contains <see cref="CompOperationTestComponent" />. </summary>
        /// <param name="scope"> The test scope that owns created assets and loaded prefab contents. </param>
        /// <returns> The prefab path, scene path, instance component, and indexed component type identifier. </returns>
        private static (
            string PrefabPath,
            string ScenePath,
            CompOperationTestComponent Component,
            string ComponentTypeId) CreateDirtyPrefabPreviewWithTestComponent (EditorTestScope scope)
        {
            var prefabPath = CreatePrefabAssetWithTestComponent(scope);
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            var component = InstantiatePrefabInstanceWithTestComponent(prefabPath);
            EditorSceneManager.MarkSceneDirty(scene);

            return (
                prefabPath,
                scenePath,
                component,
                IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)));
        }

        /// <summary> Creates a scene component whose hierarchy matches a Prefab asset but has no Unity Prefab instance linkage. </summary>
        /// <param name="scope"> The test scope that owns created assets and loaded prefab contents. </param>
        /// <returns> The prefab path, scene path, disconnected scene component, and indexed component type identifier. </returns>
        private static (
            string PrefabPath,
            string ScenePath,
            CompOperationTestComponent Component,
            string ComponentTypeId) CreateDisconnectedSceneObjectWithMatchingPrefab (EditorTestScope scope)
        {
            var prefabPath = CreatePrefabAssetWithTestComponent(scope);
            var scenePath = scope.CreateScenePath(nameof(PrefabOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PrefabRoot");
            var component = root.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);

            return (
                prefabPath,
                scenePath,
                component,
                IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)));
        }

        private static string CreatePrefabAssetWithTestComponent (EditorTestScope scope)
        {
            var prefabPath = scope.CreatePrefabAsset(nameof(PrefabOperationTests), "PrefabRoot");
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            _ = editableRoot.AddComponent<CompOperationTestComponent>();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);
            return prefabPath;
        }

        private static void SetPrefabComponentIntegerValue (
            EditorTestScope scope,
            string prefabPath,
            int value)
        {
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            var component = editableRoot.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            var serializedObject = new SerializedObject(component!);
            serializedObject.UpdateIfRequiredOrScript();
            var property = serializedObject.FindProperty("integerValue");
            Assert.That(property, Is.Not.Null);
            property!.intValue = value;
            _ = serializedObject.ApplyModifiedProperties();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);
        }

        private static CompOperationTestComponent InstantiatePrefabInstanceWithTestComponent (string prefabPath)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAsset, Is.Not.Null);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset!);
            instance.name = "InstanceRoot";
            var component = instance.GetComponent<CompOperationTestComponent>();
            Assert.That(component, Is.Not.Null);
            return component!;
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            string opName,
            object args,
            string? alias = null,
            NormalizedOperation.SourceStepKind sourceKind = NormalizedOperation.SourceStepKind.Op,
            bool suppressPersistenceReporting = false,
            bool suppressScenePersistenceReporting = false,
            bool allowExplicitPrefabAssetMutation = false)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: alias,
                Expect: null,
                SourceKind: sourceKind,
                SuppressPersistenceReporting: suppressPersistenceReporting,
                SuppressScenePersistenceReporting: suppressScenePersistenceReporting,
                AllowExplicitPrefabAssetMutation: allowExplicitPrefabAssetMutation);
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

        private static void AssertSuccess (
            OperationPhaseStepResult result,
            bool applied,
            bool changed)
        {
            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Failure, Is.Null);
        }

        private static void AssertTouchSet (
            OperationPhaseStepResult result,
            params (OperationTouchKind Kind, string Path)[] expectedTouches)
        {
            Assert.That(result.Touched.Count, Is.EqualTo(expectedTouches.Length));
            for (var i = 0; i < expectedTouches.Length; i++)
            {
                var expectedTouch = expectedTouches[i];
                Assert.That(
                    result.Touched.Any(touch =>
                        touch.Kind == expectedTouch.Kind
                        && touch.Path == expectedTouch.Path),
                    Is.True,
                    $"Touched resource was not found. kind={expectedTouch.Kind}, path={expectedTouch.Path}");
            }
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
