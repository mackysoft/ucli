using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#nullable enable

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIndexServiceTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [SetUp]
        public void SetUp ()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown ()
        {
            LogAssert.ignoreFailingMessages = false;
        }

#if UNITY_6000_0_OR_NEWER
        // NOTE: Run this test only on Unity 6000.0 or newer.
        [Test]
        [Category("Size.Small")]
        public void SerializedPropertyTypeMapper_WhenRenderingLayerMask_ReturnsRenderingLayerMaskLiteral ()
        {
            var literal = IndexSerializedPropertyTypeMapper.ToLiteral(SerializedPropertyType.RenderingLayerMask);

            Assert.That(literal, Is.EqualTo("layerMask"));
            Assert.That(
                ContractLiteralInputParser.TryParseIgnoreCase<IndexPropertyType>(literal, out var parsedType),
                Is.True);
            Assert.That(parsedType, Is.EqualTo(IndexPropertyType.LayerMask));
        }
#endif

        [Test]
        [Category("Size.Small")]
        public void DeclaredTypeResolver_WhenComponentHasNativeEnabled_ResolvesBoolean ()
        {
            var resolution = IndexDeclaredTypeResolver.Resolve(typeof(BoxCollider), "m_Enabled");

            Assert.That(resolution.IsResolved, Is.True);
            Assert.That(resolution.DeclaredType, Is.EqualTo(typeof(bool)));
            Assert.That(resolution.ElementType, Is.Null);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator AssetLookupSnapshotBuilder_Build_SortsEntriesAndExcludesFoldersAndSubassets () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var firstPath = $"Assets/zzz_asset_lookup_{token}.asset";
            var secondPath = $"Assets/aaa_asset_lookup_{token}.asset";
            var folderPath = $"Assets/asset_lookup_folder_{token}";
            scope.TrackAsset(firstPath)
                .TrackAsset(secondPath)
                .TrackAsset(folderPath);

            AssetDatabase.CreateFolder("Assets", $"asset_lookup_folder_{token}");
            var firstAsset = ScriptableObject.CreateInstance<IndexCatalogTestAsset>();
            var secondAsset = ScriptableObject.CreateInstance<IndexCatalogTestAsset>();
            var subAsset = ScriptableObject.CreateInstance<IndexCatalogTestAsset>();
            AssetDatabase.CreateAsset(firstAsset, firstPath);
            AssetDatabase.CreateAsset(secondAsset, secondPath);
            AssetDatabase.AddObjectToAsset(subAsset, firstPath);
            AssetDatabase.SaveAssets();

            var builder = new AssetLookupSnapshotBuilder();
            var response = await builder.BuildAsync(CancellationToken.None);
            var assetSearchEntries = response.AssetSearchEntries!
                .Where(entry => entry.AssetPath != null && entry.AssetPath.Contains(token, StringComparison.Ordinal))
                .ToArray();
            var guidPathEntries = response.GuidPathEntries!
                .Where(entry => entry.AssetPath != null && entry.AssetPath.Contains(token, StringComparison.Ordinal))
                .ToArray();

            Assert.That(assetSearchEntries.Length, Is.EqualTo(2));
            Assert.That(guidPathEntries.Length, Is.EqualTo(2));
            Assert.That(assetSearchEntries[0].AssetPath, Is.EqualTo(secondPath));
            Assert.That(assetSearchEntries[1].AssetPath, Is.EqualTo(firstPath));
            Assert.That(assetSearchEntries.Any(entry => entry.AssetPath == folderPath), Is.False);
            Assert.That(guidPathEntries[0].AssetPath, Is.EqualTo(secondPath));
            Assert.That(guidPathEntries[1].AssetPath, Is.EqualTo(firstPath));
            Assert.That(assetSearchEntries[0].SearchTypeIds, Does.Contain(IndexTypeIdFormatter.Format(typeof(IndexCatalogTestAsset))));
            Assert.That(assetSearchEntries[0].SearchTypeIds, Does.Contain(IndexTypeIdFormatter.Format(typeof(ScriptableObject))));
            Assert.That(assetSearchEntries[0].SearchTypeIds!.Last(), Is.EqualTo(IndexTypeIdFormatter.Format(typeof(UnityEngine.Object))));
            Assert.That(assetSearchEntries[0].AssetGuid, Is.EqualTo(guidPathEntries[0].AssetGuid));
            Assert.That(assetSearchEntries[1].AssetGuid, Is.EqualTo(guidPathEntries[1].AssetGuid));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator AssetLookupSnapshotBuilder_Build_UsesPathNameWhenMainAssetNameIsEmpty () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/empty_name_asset_lookup_{token}.asset";
            var expectedName = Path.GetFileNameWithoutExtension(assetPath);
            scope.TrackAsset(assetPath);

            var asset = ScriptableObject.CreateInstance<IndexCatalogTestAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.name = string.Empty;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var builder = new AssetLookupSnapshotBuilder();
            var response = await builder.BuildAsync(CancellationToken.None);
            var assetSearchEntry = response.AssetSearchEntries!
                .Single(entry => string.Equals(entry.AssetPath, assetPath, StringComparison.Ordinal));

            Assert.That(assetSearchEntry.Name, Is.EqualTo(expectedName));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator SceneTreeLiteSnapshotBuilder_Build_ReturnsDeterministicTree () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(UnityIndexServiceTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, worldPositionStays: false);
            var secondRoot = new GameObject("SecondRoot");
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            var rootGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(root).ToString();
            var childGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(child).ToString();
            var secondRootGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(secondRoot).ToString();
            var activeSceneBeforeBuild = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var activeSceneHandleBeforeBuild = activeSceneBeforeBuild.handle;
            var loadedSceneCountBeforeBuild = SceneManager.sceneCount;

            var builder = new SceneTreeLiteSnapshotBuilder();
            var response = await builder.BuildAsync(new UnityScenePath(scenePath), cancellationToken: CancellationToken.None);
            var roots = response.Roots;
            var activeSceneAfterBuild = SceneManager.GetActiveScene();
            var resolvedSceneAfterBuild = SceneManager.GetSceneByPath(scenePath);

            Assert.That(response.ScenePath.Value, Is.EqualTo(scenePath));
            Assert.That(response.SourceState.Kind, Is.EqualTo(SceneTreeSourceStateKind.PersistedPreview));
            Assert.That(response.SourceState.IsDirty, Is.False);
            Assert.That(roots, Is.Not.Null);
            var nonNullRoots = roots!;
            Assert.That(SceneManager.sceneCount, Is.EqualTo(loadedSceneCountBeforeBuild));
            Assert.That(activeSceneAfterBuild.handle, Is.EqualTo(activeSceneHandleBeforeBuild));
            Assert.That(resolvedSceneAfterBuild.IsValid(), Is.False);
            Assert.That(nonNullRoots.Count, Is.EqualTo(2));
            Assert.That(nonNullRoots[0].Name, Is.EqualTo("Root"));
            Assert.That(nonNullRoots[0].GlobalObjectId, Is.EqualTo(rootGlobalObjectId));
            Assert.That(nonNullRoots[0].Children, Is.Not.Null);
            var firstRootChildren = nonNullRoots[0].Children!;
            Assert.That(firstRootChildren.Count, Is.EqualTo(1));
            Assert.That(firstRootChildren[0].Name, Is.EqualTo("Child"));
            Assert.That(firstRootChildren[0].GlobalObjectId, Is.EqualTo(childGlobalObjectId));
            Assert.That(nonNullRoots[1].Name, Is.EqualTo("SecondRoot"));
            Assert.That(nonNullRoots[1].GlobalObjectId, Is.EqualTo(secondRootGlobalObjectId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator SceneTreeLiteSnapshotBuilder_Build_WhenLoadedSceneOnlyAndSceneIsNotLoaded_FailsWithoutOpeningPreview () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(UnityIndexServiceTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("PersistedRoot");
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            var activeSceneBeforeBuild = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var activeSceneHandleBeforeBuild = activeSceneBeforeBuild.handle;
            var loadedSceneCountBeforeBuild = SceneManager.sceneCount;
            var builder = new SceneTreeLiteSnapshotBuilder();

            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentException>(async () =>
            {
                await builder.BuildAsync(new UnityScenePath(scenePath), loadedSceneOnly: true, cancellationToken: CancellationToken.None).AsUniTask();
            }, "Loaded-scene-only scene-tree-lite read", AsyncWaitTimeout);

            Assert.That(exception.ParamName, Is.EqualTo("scenePath"));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(loadedSceneCountBeforeBuild));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeSceneHandleBeforeBuild));
            Assert.That(SceneManager.GetSceneByPath(scenePath).IsValid(), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator SceneTreeLiteSnapshotBuilder_Build_WhenSceneIsLoadedDirty_ReturnsLoadedDirtyTree () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(UnityIndexServiceTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("SavedRoot");
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            _ = new GameObject("UnsavedRoot");
            EditorSceneManager.MarkSceneDirty(scene);

            var builder = new SceneTreeLiteSnapshotBuilder();
            var response = await builder.BuildAsync(new UnityScenePath(scenePath), cancellationToken: CancellationToken.None);
            var roots = response.Roots!;

            Assert.That(response.SourceState.Kind, Is.EqualTo(SceneTreeSourceStateKind.LoadedScene));
            Assert.That(response.SourceState.IsDirty, Is.True);
            Assert.That(roots.Select(static root => root.Name), Is.EquivalentTo(new[] { "SavedRoot", "UnsavedRoot" }));
        });
    }
}
