using System;
using System.Collections;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
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
    public sealed class ResolveOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public void ContractMapper_WhenSelectorUsesTypedSceneComponentValues_PreservesSemanticValues ()
        {
            var scene = new SceneAssetPath("Assets/Sample.unity");
            var hierarchyPath = new UnityHierarchyPath("Root/Child");
            var componentType = new UnityComponentTypeId("Example.Component");
            var args = new ResolveSelectorArgs(
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scene: scene,
                prefab: null,
                hierarchyPath: hierarchyPath,
                componentType: componentType);

            var result = UnityObjectReferenceContractMapper.TryMap(args, out var selector, out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(selector!.Kind, Is.EqualTo(ResolveSelectorKind.SceneComponent));
            Assert.That(selector.ScenePath, Is.SameAs(scene));
            Assert.That(selector.HierarchyPath, Is.SameAs(hierarchyPath));
            Assert.That(selector.ComponentType, Is.SameAs(componentType));
        }

        [Test]
        [Category("Size.Small")]
        public void ContractMapper_WhenReferenceUsesTypedAlias_PreservesSemanticValue ()
        {
            var alias = new UcliPlanAlias("target");
            var args = new GameObjectReferenceArgs(
                alias: alias,
                globalObjectId: null,
                prefab: null,
                scene: null,
                hierarchyPath: null);

            var result = UnityObjectReferenceContractMapper.TryMap(
                args,
                "args.target",
                OperationAliasReferenceMap.Empty,
                out var reference,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(reference!.Kind, Is.EqualTo(UnityObjectReferenceKind.Alias));
            Assert.That(reference.Alias, Is.EqualTo(RequestLocalAliasIdentity.FromPublicAlias(alias)));
            Assert.That(reference.Alias!.Alias, Is.SameAs(alias));
        }

        [TestCase("{\"assetGuid\":\"not-a-guid\"}", IpcResolveSelectorPropertyNames.AssetGuid)]
        [TestCase("{\"assetGuid\":\"00000000-0000-0000-0000-000000000000\"}", IpcResolveSelectorPropertyNames.AssetGuid)]
        [TestCase("{\"assetPath\":\"ProjectSettings/TagManager.asset\"}", IpcResolveSelectorPropertyNames.AssetPath)]
        [TestCase("{\"projectAssetPath\":\"Assets/Sample.asset\"}", IpcResolveSelectorPropertyNames.ProjectAssetPath)]
        [TestCase("{\"scene\":\"Assets/Sample.prefab\",\"hierarchyPath\":\"Root\"}", IpcResolveSelectorPropertyNames.Scene)]
        [TestCase("{\"prefab\":\"Assets/Sample.unity\",\"hierarchyPath\":\"Root\"}", IpcResolveSelectorPropertyNames.Prefab)]
        [TestCase("{\"scene\":\"Assets/Sample.unity\",\"hierarchyPath\":\"Root//Child\"}", IpcResolveSelectorPropertyNames.HierarchyPath)]
        [Category("Size.Small")]
        public void ResolveSelectorCodec_WhenRawValueViolatesSemanticContract_ReturnsFalse (
            string json,
            string propertyName)
        {
            using var document = JsonDocument.Parse(json);

            var result = ResolveSelectorCodec.TryParse(document.RootElement, out var selector, out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(selector, Is.Null);
            Assert.That(errorMessage, Does.Contain(propertyName));
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveSelector_WhenAssetGuidIsEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() => ResolveSelector.FromAssetGuid(Guid.Empty));

            Assert.That(exception!.ParamName, Is.EqualTo("assetGuid"));
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveSelectorCodec_WhenAssetGuidUsesStandardJsonFormat_ProjectsNativeGuid ()
        {
            using var document = JsonDocument.Parse(
                "{\"assetGuid\":\"11111111-1111-1111-1111-111111111111\"}");

            var result = ResolveSelectorCodec.TryParse(document.RootElement, out var selector, out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(selector!.AssetGuid, Is.EqualTo(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveSelectorCodec_WhenRawSceneComponentSelectorIsValid_ProjectsToSemanticValues ()
        {
            using var document = JsonDocument.Parse(
                "{\"scene\":\"Assets/Sample.unity\",\"hierarchyPath\":\"Root/Child\",\"componentType\":\"Example.Component\"}");

            var result = ResolveSelectorCodec.TryParse(document.RootElement, out var selector, out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(selector!.Kind, Is.EqualTo(ResolveSelectorKind.SceneComponent));
            Assert.That(selector.ScenePath, Is.EqualTo(new SceneAssetPath("Assets/Sample.unity")));
            Assert.That(selector.HierarchyPath, Is.EqualTo(new UnityHierarchyPath("Root/Child")));
            Assert.That(selector.ComponentType, Is.EqualTo(new UnityComponentTypeId("Example.Component")));
        }

        [Test]
        [Category("Size.Small")]
        public void UnityObjectReferenceCodec_WhenRawAliasIsValid_ProjectsToTypedAlias ()
        {
            using var document = JsonDocument.Parse("{\"var\":\"target\"}");

            var result = UnityObjectReferenceCodec.TryParse(
                document.RootElement,
                "args.target",
                OperationAliasReferenceMap.Empty,
                out var reference,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(reference!.Kind, Is.EqualTo(UnityObjectReferenceKind.Alias));
            Assert.That(
                reference.Alias,
                Is.EqualTo(RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias("target"))));
            Assert.That(reference.Alias!.Alias, Is.EqualTo(new UcliPlanAlias("target")));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenArgsContainMultipleSelectors_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    assetPath = "Assets/sample.asset",
                    assetGuid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenGlobalObjectIdIsMalformed_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    globalObjectId = "invalid-global-object-id",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainGlobalObjectId_StoresCanonicalIdentityToAliasStore () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out _);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    globalObjectId = expectedGlobalObjectId,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            AssertResolvedGlobalObjectId(result, expectedGlobalObjectId);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [Test]
        [Category("Size.Small")]
        public void TryResolveStableReference_WhenIdentifierTypeHasLeadingZero_UsesCanonicalRequestLocalKey ()
        {
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out _);
            var canonicalGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var nonCanonicalGlobalObjectId = GlobalObjectIdTestValues.CreateNonCanonicalIdentifierTypeText(canonicalGlobalObjectId);
            Assert.That(GlobalObjectId.TryParse(nonCanonicalGlobalObjectId, out var parsedGlobalObjectId), Is.True);
            Assert.That(parsedGlobalObjectId.ToString(), Is.EqualTo(canonicalGlobalObjectId));
            Assert.That(nonCanonicalGlobalObjectId, Is.Not.EqualTo(canonicalGlobalObjectId));
            var context = scope.CreateExecutionContext();
            context.MarkDeletedStableObject(new UnityGlobalObjectId(canonicalGlobalObjectId));

            var result = ResolveReferenceResolver.TryResolveStableReference(
                ResolveSelector.FromGlobalObjectId(new UnityGlobalObjectId(nonCanonicalGlobalObjectId)),
                context,
                allowTemporaryState: true,
                out var resolvedReference,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(resolvedReference, Is.Null);
            Assert.That(errorMessage, Does.Contain(canonicalGlobalObjectId));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveUnityObject_WhenAssetShadowKeyHasEquivalentSpelling_ReturnsShadow ()
        {
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out var assetPath);
            var canonicalGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var nonCanonicalGlobalObjectId = GlobalObjectIdTestValues.CreateNonCanonicalIdentifierTypeText(canonicalGlobalObjectId);
            var shadow = ScriptableObject.CreateInstance<ResolveTestAsset>();
            var context = scope.CreateExecutionContext();
            context.TrackTemporaryObject(shadow);
            context.SetAssetShadow(
                new UnityGlobalObjectId(canonicalGlobalObjectId),
                shadow,
                assetPath);

            var result = ResolveReferenceResolver.TryResolveUnityObject(
                ResolveSelector.FromGlobalObjectId(new UnityGlobalObjectId(nonCanonicalGlobalObjectId)),
                context,
                allowTemporaryState: true,
                out var unityObject,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(unityObject, Is.SameAs(shadow));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveUnityObject_WhenDirtyScenePreviewKeyHasEquivalentSpelling_ReturnsPreviewObject ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var canonicalGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(root).ToString();
            var nonCanonicalGlobalObjectId = GlobalObjectIdTestValues.CreateNonCanonicalIdentifierTypeText(canonicalGlobalObjectId);
            root.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var result = ResolveReferenceResolver.TryResolveUnityObject(
                ResolveSelector.FromGlobalObjectId(new UnityGlobalObjectId(nonCanonicalGlobalObjectId)),
                context,
                allowTemporaryState: true,
                out var unityObject,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(unityObject, Is.TypeOf<GameObject>());
            Assert.That(unityObject, Is.Not.SameAs(root));
            Assert.That(unityObject!.name, Is.EqualTo("Renamed"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolve_WhenStableAliasAllowsTemporaryState_ReturnsPreviewObject ()
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var resolvedReference = UnityObjectReferenceResolver.CreateGlobalObjectId(root);
            root.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var context = scope.CreateExecutionContext();
            context.AliasStore.Set("target", resolvedReference);
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);

            var previewResult = UnityObjectReferenceResolver.TryResolve(
                UnityObjectReference.FromAlias(
                    RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias("target"))),
                context,
                allowTemporaryState: true,
                out var previewObject,
                out var previewErrorMessage);
            Assert.That(previewResult, Is.True, previewErrorMessage);
            Assert.That(previewObject, Is.Not.SameAs(root));
            Assert.That(previewObject!.name, Is.EqualTo("Renamed"));
        }

        [TestCase((int)OperationObjectReferenceUtilities.ReferenceResolutionPolicy.LiveOnly, false)]
        [TestCase((int)OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryAliases, false)]
        [TestCase((int)OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState, true)]
        [Category("Size.Small")]
        public void OperationReferenceResolution_WhenStableAndTemporaryAliasesExist_HonorsPolicyPrecedence (
            int resolutionPolicyValue,
            bool expectsTemporaryObject)
        {
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out var assetPath);
            var stableGlobalObjectId = UnityObjectReferenceResolver.CreateGlobalObjectId(asset);
            var shadow = ScriptableObject.CreateInstance<ResolveTestAsset>();
            var context = scope.CreateExecutionContext();
            context.TrackTemporaryObject(shadow);
            context.AliasStore.Set("target", stableGlobalObjectId);
            context.SetTemporaryAlias(
                "target",
                shadow,
                OperationResource.PersistentAsset(assetPath),
                RequestLocalObjectIdentity.FromGlobalObjectId(stableGlobalObjectId));

            var result = OperationObjectReferenceUtilities.TryResolveUnityObject(
                UnityObjectReference.FromAlias(
                    RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias("target"))),
                context,
                (OperationObjectReferenceUtilities.ReferenceResolutionPolicy)resolutionPolicyValue,
                out var resolution,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(resolution.UnityObject, Is.SameAs(expectsTemporaryObject ? shadow : asset));
            Assert.That(
                resolution.TemporaryAliasResource.HasValue,
                Is.EqualTo(expectsTemporaryObject));
        }

        [TestCase((int)OperationObjectReferenceUtilities.ReferenceResolutionPolicy.LiveOnly, false)]
        [TestCase((int)OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryAliases, true)]
        [TestCase((int)OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState, true)]
        [Category("Size.Small")]
        public void OperationReferenceResolution_WhenOnlyTemporaryAliasExists_HonorsPolicyEligibility (
            int resolutionPolicyValue,
            bool expectsSuccess)
        {
            using var scope = new EditorTestScope();
            var shadow = ScriptableObject.CreateInstance<ResolveTestAsset>();
            var context = scope.CreateExecutionContext();
            context.TrackTemporaryObject(shadow);
            context.SetTemporaryAlias(
                "target",
                shadow,
                OperationResource.PersistentAsset("Assets/Temporary.asset"));

            var result = OperationObjectReferenceUtilities.TryResolveUnityObject(
                UnityObjectReference.FromAlias(
                    RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias("target"))),
                context,
                (OperationObjectReferenceUtilities.ReferenceResolutionPolicy)resolutionPolicyValue,
                out var resolution,
                out var errorMessage);

            Assert.That(result, Is.EqualTo(expectsSuccess), errorMessage);
            Assert.That(resolution.UnityObject, expectsSuccess ? Is.SameAs(shadow) : Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void OperationReferenceResolution_WhenStableAndTemporaryAliasSourcesDiffer_RejectsBinding ()
        {
            using var scope = new EditorTestScope();
            var stableAsset = scope.CreateScriptableAsset<ResolveTestAsset>(
                $"{nameof(ResolveOperationTests)}_stable",
                out _);
            var temporarySourceAsset = scope.CreateScriptableAsset<ResolveTestAsset>(
                $"{nameof(ResolveOperationTests)}_temporary",
                out var temporarySourcePath);
            var context = scope.CreateExecutionContext();
            context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateGlobalObjectId(stableAsset));
            context.SetTemporaryAlias(
                "target",
                temporarySourceAsset,
                OperationResource.PersistentAsset(temporarySourcePath),
                RequestLocalObjectIdentity.FromUnityObject(temporarySourceAsset));

            var result = OperationObjectReferenceUtilities.TryResolveUnityObject(
                UnityObjectReference.FromAlias(
                    RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias("target"))),
                context,
                OperationObjectReferenceUtilities.ReferenceResolutionPolicy.AllowTemporaryState,
                out _,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Does.Contain("inconsistent stable and request-local source identities"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolve_WhenStableAliasWasDeletedInTemporaryState_ReturnsFalse ()
        {
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out _);
            var resolvedReference = UnityObjectReferenceResolver.CreateGlobalObjectId(asset);
            var context = scope.CreateExecutionContext();
            context.AliasStore.Set("target", resolvedReference);
            context.MarkDeletedStableObject(resolvedReference);

            var result = UnityObjectReferenceResolver.TryResolve(
                UnityObjectReference.FromAlias(
                    RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias("target"))),
                context,
                allowTemporaryState: true,
                out var unityObject,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(unityObject, Is.Null);
            Assert.That(errorMessage, Does.Contain(resolvedReference.Value));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainAssetGuid_ResolvesMainAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out var assetPath);
            var assetGuid = Guid.ParseExact(AssetDatabase.AssetPathToGUID(assetPath), "N");
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    assetGuid,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainAssetPath_ResolvesMainAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out var assetPath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    assetPath,
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainProjectAssetPath_ResolvesMainAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            var projectAssetPath = "ProjectSettings/TagManager.asset";
            var projectAsset = AssetDatabase.LoadMainAssetAtPath(projectAssetPath);
            Assert.That(projectAsset, Is.Not.Null);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(projectAsset).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    projectAssetPath,
                });
            using var context = new OperationExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainSceneHierarchyPath_ResolvesGameObjectInLoadedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
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
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSceneIsNotLoaded_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
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

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenArgsContainSceneComponentSelector_ResolvesComponentInLoadedScene () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var targetComponent = root.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(targetComponent).ToString();
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSceneTargetWasDeletedInRequestLocalState_GlobalObjectIdResolveFails () => UniTask.ToCoroutine(async () =>
        {
            var deleteOperation = new GoDeleteOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var deletedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(child).ToString();
            var context = scope.CreateExecutionContext();
            var deleteRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("op-delete"), primitiveIndex: 0),
                Op: UcliPrimitiveOperationNames.GoDelete,
                Args: JsonSerializer.SerializeToElement(new
                {
                    target = new
                    {
                        globalObjectId = deletedGlobalObjectId,
                    },
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    globalObjectId = deletedGlobalObjectId,
                });

            var deleteResult = await deleteOperation.PlanAsync(deleteRequest, context, CancellationToken.None);
            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(deleteResult.IsSuccess, Is.True, deleteResult.Failure?.Message);
            Assert.That(deleteResult.Applied, Is.False);
            Assert.That(deleteResult.Changed, Is.True);
            Assert.That(deleteResult.Touched.Count, Is.EqualTo(1));
            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTemporarySceneExists_ResolvesStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(child).ToString();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryGetOrOpenTemporaryScene(scenePath, out var previewScene, out var previewErrorMessage), Is.True, previewErrorMessage);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Child",
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTemporaryPrefabRootExists_ResolvesStableReference () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot");
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("op-open"), primitiveIndex: 0),
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            if (temporaryRoot == null)
            {
                throw new AssertionException("Expected the temporary prefab contents root to be available.");
            }

            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(temporaryRoot, out var temporaryRootReference), Is.True);
            Assert.That(context.TryResolveTemporaryPrefabGlobalObjectId(prefabPath, temporaryRoot, out var temporaryRootGlobalObjectId), Is.True);
            Assert.That(temporaryRootGlobalObjectId!.Value, Is.EqualTo(temporaryRootReference!.Value));
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = temporaryRoot.name,
                });
            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(openResult.IsSuccess, Is.True);
            Assert.That(resolveResult.IsSuccess, Is.True, resolveResult.Failure?.Message);
            Assert.That(resolveResult.Applied, Is.False);
            Assert.That(resolveResult.Changed, Is.False);
            Assert.That(resolveResult.Failure, Is.Null);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference, Is.EqualTo(temporaryRootGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenArgsContainPrefabComponentSelector_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot");
            var editableRoot = scope.LoadPrefabContents(prefabPath);
            Assert.That(editableRoot, Is.Not.Null);
            _ = editableRoot.AddComponent<CompOperationTestComponent>();
            _ = PrefabUtility.SaveAsPrefabAsset(editableRoot, prefabPath);
            scope.UnloadPrefabContents(editableRoot);

            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var targetComponent = prefabAssetRoot!.GetComponent<CompOperationTestComponent>();
            Assert.That(targetComponent, Is.Not.Null);
            AssetDatabase.SaveAssets();
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("op-open")),
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });

            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(openResult.IsSuccess, Is.True);
            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTrackedPreviewPrefabTargetHasNoStableGlobalObjectId_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(persistedChild!.gameObject, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            var openRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("op-open"), primitiveIndex: 0),
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);

            var previewExistingChild = temporaryRoot!.transform.Find("Child");
            Assert.That(previewExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(previewExistingChild!.gameObject);

            var previewChild = new GameObject("Child");
            previewChild.transform.SetParent(temporaryRoot.transform, worldPositionStays: false);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewChild, out _), Is.False);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Child",
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyOpenedPrefabStageIsMirrored_DoesNotInventStableReference () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = prefabStage!.prefabContentsRoot.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);
            stageChild!.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var expectedTarget = prefabAssetRoot!.transform.Find("Child");
            Assert.That(expectedTarget, Is.Not.Null);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(expectedTarget!.gameObject).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChild = temporaryRoot!.transform.Find("Renamed");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewChild!.gameObject, out _), Is.False);
            var hierarchyPath = $"{temporaryRoot.name}/Renamed";

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath,
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyOpenedPrefabStageInsertsSiblingBeforeExistingChild_DoesNotInventStableReference () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "ChildA", "ChildB");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);
            Assert.That(stageRoot!.transform.Find("ChildA"), Is.Not.Null);

            var insertedChild = new GameObject("Inserted");
            insertedChild.transform.SetParent(stageRoot.transform, worldPositionStays: false);
            insertedChild.transform.SetSiblingIndex(0);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var expectedTarget = prefabAssetRoot!.transform.Find("ChildA");
            Assert.That(expectedTarget, Is.Not.Null);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(expectedTarget!.gameObject).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChildA = temporaryRoot!.transform.Find("ChildA");
            Assert.That(previewChildA, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewChildA!.gameObject, out _), Is.False);
            var hierarchyPath = $"{temporaryRoot.name}/ChildA";

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath,
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenPrefabTargetWasDeletedInRequestLocalState_GlobalObjectIdResolveFails () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var deleteOperation = new GoDeleteOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("op-open"), primitiveIndex: 0),
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var openResult = await openOperation.PlanAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True, openResult.Failure?.Message);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);
            var previewChild = temporaryRoot!.transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewChild!.gameObject, out var previewChildReference), Is.True);
            var deleteRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForEditPrimitive(new IpcExecuteStepId("op-delete"), primitiveIndex: 0),
                Op: UcliPrimitiveOperationNames.GoDelete,
                Args: JsonSerializer.SerializeToElement(new
                {
                    target = new
                    {
                        globalObjectId = previewChildReference!.Value,
                    },
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    globalObjectId = previewChildReference!.Value,
                });
            var deleteResult = await deleteOperation.PlanAsync(deleteRequest, context, CancellationToken.None);
            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            Assert.That(deleteResult.IsSuccess, Is.True, deleteResult.Failure?.Message);
            Assert.That(deleteResult.Applied, Is.False);
            Assert.That(deleteResult.Changed, Is.True);
            Assert.That(deleteResult.Touched.Count, Is.EqualTo(1));
            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenOpenedPrefabStageAlreadyDeletedPersistedChild_GlobalObjectIdResolveFails () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            var deletedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(persistedChild!.gameObject).ToString();

            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = prefabStage!.prefabContentsRoot.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(stageChild!.gameObject);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    globalObjectId = deletedGlobalObjectId,
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenOpenedPrefabStageTargetHasNoStableGlobalObjectId_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(persistedChild!.gameObject, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            var openRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("op-open")),
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);
            Assert.That(openResult.Applied, Is.True);
            Assert.That(openResult.Changed, Is.False);
            Assert.That(openResult.Failure, Is.Null);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);

            var stageExistingChild = stageRoot!.transform.Find("Child");
            Assert.That(stageExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(stageExistingChild!.gameObject);

            var stageChild = new GameObject("Child");
            stageChild.transform.SetParent(stageRoot.transform, worldPositionStays: false);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(stageChild, out _), Is.False);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Child",
                });

            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenOpenedPrefabStageTargetHasNoExplicitStableMapping_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefabAssetRoot, Is.Not.Null);
            var persistedChild = prefabAssetRoot!.transform.Find("Child");
            Assert.That(persistedChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(persistedChild!.gameObject, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            var openRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("op-open")),
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(prefabStage, Is.Not.Null);
            var stageChild = prefabStage!.prefabContentsRoot.transform.Find("Child");
            Assert.That(stageChild, Is.Not.Null);
            stageChild!.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(stageChild.gameObject, out _), Is.False);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(stageChild, out _), Is.False);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Renamed",
                });

            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyOpenedPrefabStageHasAmbiguousSiblingNames_DoesNotGuessStableSourceMapping () => UniTask.ToCoroutine(async () =>
        {
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child", "Child");
            var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);

            var insertedChild = new GameObject("Inserted");
            insertedChild.transform.SetParent(stageRoot.transform, worldPositionStays: false);
            insertedChild.transform.SetSiblingIndex(0);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);

            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsurePrefabExecutionSession(prefabPath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryPrefabContentsRoot(prefabPath, out var temporaryRoot), Is.True);
            Assert.That(temporaryRoot, Is.Not.Null);

            var previewFirstChild = temporaryRoot!.transform.GetChild(1).gameObject;
            Assert.That(previewFirstChild.name, Is.EqualTo("Child"));
            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = $"{temporaryRoot.name}/Child",
                });

            var resolveResult = await resolveOperation.PlanAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenOpenedPrefabStageTargetIsMissing_DoesNotFallbackToPersistedPrefabAsset () => UniTask.ToCoroutine(async () =>
        {
            var openOperation = new PrefabOpenOperation();
            var resolveOperation = new ResolveOperation();
            using var scope = new EditorTestScope()
                .EnablePrefabStageCleanup();
            var prefabPath = scope.CreatePrefabAsset(nameof(ResolveOperationTests), "PrefabRoot", "Child");
            var context = scope.CreateExecutionContext();
            var openRequest = new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId("op-open")),
                Op: UcliPrimitiveOperationNames.PrefabOpen,
                Args: JsonSerializer.SerializeToElement(new
                {
                    path = prefabPath,
                }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
            var openResult = await openOperation.CallAsync(openRequest, context, CancellationToken.None);
            Assert.That(openResult.IsSuccess, Is.True);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.That(prefabStage, Is.Not.Null);
            var stageRoot = prefabStage!.prefabContentsRoot;
            Assert.That(stageRoot, Is.Not.Null);

            var stageExistingChild = stageRoot!.transform.Find("Child");
            Assert.That(stageExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(stageExistingChild!.gameObject);

            var resolveRequest = CreateOperation(
                opId: "op-resolve",
                alias: "resolved",
                args: new
                {
                    prefab = prefabPath,
                    hierarchyPath = "PrefabRoot/Child",
                });

            var resolveResult = await resolveOperation.CallAsync(resolveRequest, context, CancellationToken.None);

            AssertInvalidArgument(resolveResult, "op-resolve");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenTrackedPreviewSceneTargetHasNoStableGlobalObjectId_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var context = scope.CreateExecutionContext();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var persistedChild = new GameObject("Child");
            persistedChild.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(persistedChild, out var persistedReference), Is.True);
            Assert.That(persistedReference, Is.Not.Null);

            Assert.That(context.TryGetOrOpenTemporaryScene(scenePath, out var previewScene, out var previewErrorMessage), Is.True, previewErrorMessage);
            var previewRoot = FindRootGameObject(previewScene, "Root");
            var previewExistingChild = previewRoot.transform.Find("Child");
            Assert.That(previewExistingChild, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(previewExistingChild!.gameObject);

            var previewChild = new GameObject("Child");
            previewChild.transform.SetParent(previewRoot.transform, worldPositionStays: false);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewChild, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Child",
                },
                alias: "resolved");

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyLoadedSceneIsMirrored_FallsBackToLiveObjectStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            EditorSceneManager.SaveScene(scene, scenePath);

            child.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(child).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Renamed");
            Assert.That(previewChild, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewChild!.gameObject, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Renamed",
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyLoadedSceneComponentIsMirrored_FallsBackToLiveComponentStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var component = child.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);

            child.name = "Renamed";
            EditorSceneManager.MarkSceneDirty(scene);
            var expectedGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Renamed");
            Assert.That(previewChild, Is.Not.Null);
            var previewComponent = previewChild!.GetComponent<CompOperationTestComponent>();
            Assert.That(previewComponent, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewComponent, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Renamed",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
            Assert.That(resolvedReference!.Value, Is.EqualTo(expectedGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenDirtyLoadedSceneComponentWasRecreated_DoesNotGuessStableReference () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var persistedComponent = child.AddComponent<CompOperationTestComponent>();
            EditorSceneManager.SaveScene(scene, scenePath);

            UnityEngine.Object.DestroyImmediate(persistedComponent);
            var recreatedComponent = child.AddComponent<CompOperationTestComponent>();
            Assert.That(recreatedComponent, Is.Not.Null);
            EditorSceneManager.MarkSceneDirty(scene);
            var context = scope.CreateExecutionContext();
            Assert.That(context.TryEnsureSceneExecutionSession(scenePath, out var ensureErrorMessage), Is.True, ensureErrorMessage);
            Assert.That(context.TryGetTemporaryScene(scenePath, out var previewScene), Is.True);
            var previewChild = FindRootGameObject(previewScene, "Root").transform.Find("Child");
            Assert.That(previewChild, Is.Not.Null);
            var previewComponent = previewChild!.GetComponent<CompOperationTestComponent>();
            Assert.That(previewComponent, Is.Not.Null);
            Assert.That(UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(previewComponent, out _), Is.False);

            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    scene = scenePath,
                    hierarchyPath = "Root/Child",
                    componentType = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                });

            var result = await operation.PlanAsync(requestOperation, context, CancellationToken.None);

            if (UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(recreatedComponent, out var recreatedReference))
            {
                AssertSuccess(result, applied: false, changed: false);
                Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
                Assert.That(resolvedReference!.Value, Is.EqualTo(recreatedReference!.Value));
                return;
            }

            AssertInvalidArgument(result, "op-1");
            Assert.That(context.AliasStore.TryGet("resolved", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenHierarchyPathResolvesNoObject_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
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

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenHierarchyPathResolvesMultipleObjects_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ResolveOperationTests));
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

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-1");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenResolutionSucceeds_ReturnsAppliedFalseAndChangedFalse () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ResolveOperation();
            using var scope = new EditorTestScope();
            var asset = scope.CreateScriptableAsset<ResolveTestAsset>(nameof(ResolveOperationTests), out _);
            var requestOperation = CreateOperation(
                opId: "op-1",
                alias: "resolved",
                args: new
                {
                    globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString(),
                });
            var context = scope.CreateExecutionContext();

            var result = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertSuccess(result, applied: false, changed: false);
            Assert.That(context.AliasStore.TryGet("resolved", out var resolvedReference), Is.True);
            Assert.That(resolvedReference, Is.Not.Null);
        });

        private static GameObject FindRootGameObject (
            Scene scene,
            string name)
        {
            var rootGameObjects = scene.GetRootGameObjects();
            for (var i = 0; i < rootGameObjects.Length; i++)
            {
                if (rootGameObjects[i].name == name)
                {
                    return rootGameObjects[i];
                }
            }

            Assert.Fail($"Root GameObject was not found: {name}.");
            return null!;
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            object args,
            string? alias = null)
        {
            return new NormalizedOperation(
                ExecutionKey: OperationExecutionKey.ForRawStep(new IpcExecuteStepId(opId)),
                Op: UcliPrimitiveOperationNames.Resolve,
                Args: JsonSerializer.SerializeToElement(args),
                As: alias == null
                    ? null
                    : RequestLocalAliasIdentity.FromPublicAlias(new UcliPlanAlias(alias)),
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
        }

        private static void AssertInvalidArgument (
            OperationPhaseStepResult result,
            string expectedOperationId)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Failure.OpId?.Value, Is.EqualTo(expectedOperationId));
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

        private static void AssertResolvedGlobalObjectId (
            OperationPhaseStepResult result,
            string expectedGlobalObjectId)
        {
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Result!.Value.GetProperty("globalObjectId").GetString(), Is.EqualTo(expectedGlobalObjectId));
        }

        private sealed class ResolveTestAsset : ScriptableObject
        {
        }
    }
}
