using MackySoft.Ucli.Infrastructure.Index;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Project;
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
    public sealed class IndexCatalogBuilderTests
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

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Build_WhenRequiredInputsExist_ReturnsCatalogsAndContainsExpectedEntries () => UniTask.ToCoroutine(async () =>
        {
            var builder = CreateBuilder(
                new SuccessComponentSchemaExtractor(),
                new SuccessAssetSchemaExtractor(),
                new SuccessIndexInputFingerprintCalculator());

            var result = await builder.BuildAsync(ResolveProjectRootPath(), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ErrorMessage, Is.Null);
            Assert.That(result.TypesCatalog, Is.Not.Null);
            Assert.That(result.SchemasCatalog, Is.Not.Null);
            Assert.That(result.InputsManifest, Is.Not.Null);
            Assert.That(result.TypesCatalog!.Entries, Is.Not.Null);
            Assert.That(result.SchemasCatalog!.Entries, Is.Not.Null);

            var componentTypeId = IndexTypeIdFormatter.Format(typeof(BoxCollider));
            var assetTypeId = IndexTypeIdFormatter.Format(typeof(IndexCatalogTestAsset));

            var componentTypeEntry = result.TypesCatalog.Entries!.SingleOrDefault(entry => entry.TypeId == componentTypeId);
            Assert.That(componentTypeEntry, Is.Not.Null);
            Assert.That(componentTypeEntry!.Flags, Is.Not.Null);
            Assert.That(componentTypeEntry.Flags!.IsComponent, Is.True);

            var assetTypeEntry = result.TypesCatalog.Entries!.SingleOrDefault(entry => entry.TypeId == assetTypeId);
            Assert.That(assetTypeEntry, Is.Not.Null);
            Assert.That(assetTypeEntry!.Flags, Is.Not.Null);
            Assert.That(assetTypeEntry.Flags!.IsScriptableObject, Is.True);

            var componentSchema = result.SchemasCatalog.Entries!.SingleOrDefault(entry =>
                entry.Kind == "comp"
                && entry.TypeId == componentTypeId);
            Assert.That(componentSchema, Is.Not.Null);
            Assert.That(componentSchema!.SchemaKey, Is.EqualTo($"{"comp"}:{componentTypeId}"));
            var enabledProperty = componentSchema.Properties!.SingleOrDefault(property => property.Path == "m_Enabled");
            Assert.That(enabledProperty, Is.Not.Null);
            Assert.That(enabledProperty!.PropertyType, Is.EqualTo("boolean"));
            Assert.That(enabledProperty.DeclaredTypeId, Is.EqualTo(IndexTypeIdFormatter.Format(typeof(bool))));

            var assetSchema = result.SchemasCatalog.Entries!.SingleOrDefault(entry =>
                entry.Kind == "asset"
                && entry.TypeId == assetTypeId);
            Assert.That(assetSchema, Is.Not.Null);
            Assert.That(assetSchema!.SchemaKey, Is.EqualTo($"{"asset"}:{assetTypeId}"));
            var speedProperty = assetSchema.Properties!.SingleOrDefault(property => property.Path == "speed");
            Assert.That(speedProperty, Is.Not.Null);
            Assert.That(speedProperty!.PropertyType, Is.EqualTo("float"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Build_WhenExtractorsReturnBothKinds_MergesAndOrdersSchemas () => UniTask.ToCoroutine(async () =>
        {
            var builder = CreateBuilder(
                new SuccessComponentSchemaExtractor(),
                new SuccessAssetSchemaExtractor(),
                new SuccessIndexInputFingerprintCalculator());

            var result = await builder.BuildAsync(ResolveProjectRootPath(), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.SchemasCatalog, Is.Not.Null);
            Assert.That(result.SchemasCatalog!.Entries, Is.Not.Null);
            Assert.That(result.SchemasCatalog.Entries!.Count, Is.EqualTo(2));
            Assert.That(result.SchemasCatalog.Entries[0].Kind, Is.EqualTo("asset"));
            Assert.That(result.SchemasCatalog.Entries[1].Kind, Is.EqualTo("comp"));
            Assert.That(
                StringComparer.Ordinal.Compare(
                    result.SchemasCatalog.Entries[0].SchemaKey ?? string.Empty,
                    result.SchemasCatalog.Entries[1].SchemaKey ?? string.Empty),
                Is.LessThanOrEqualTo(0));
        });

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

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Extractors_WhenCancellationRequested_ThrowOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var propertyCollector = new IndexSchemaPropertyCollector();
            var componentExtractor = new ComponentSchemaExtractor(propertyCollector);
            var assetExtractor = new AssetSchemaExtractor(propertyCollector);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await componentExtractor.ExtractAsync(
                    new[] { typeof(BoxCollider) },
                    cts.Token);
            }, "Canceled component extractor", AsyncWaitTimeout);

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await assetExtractor.ExtractAsync(
                    new[] { typeof(GUISkin) },
                    cts.Token);
            }, "Canceled asset extractor", AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Extractors_WhenCollectorThrows_PropagateException () => UniTask.ToCoroutine(async () =>
        {
            var componentExtractor = new ComponentSchemaExtractor(new ThrowingPropertyCollector());
            var assetExtractor = new AssetSchemaExtractor(new ThrowingPropertyCollector());

            async UniTask RunComponentExtractAsync ()
            {
                await componentExtractor.ExtractAsync(
                    new[] { typeof(BoxCollider) },
                    CancellationToken.None);
            }

            async UniTask RunAssetExtractAsync ()
            {
                await assetExtractor.ExtractAsync(
                    new[] { typeof(GUISkin) },
                    CancellationToken.None);
            }

            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(
                RunComponentExtractAsync,
                "Component extractor failure propagation",
                AsyncWaitTimeout);
            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(
                RunAssetExtractAsync,
                "Asset extractor failure propagation",
                AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Extractors_WhenAssetTypeIsEditorAssembly_SkipsExtraction () => UniTask.ToCoroutine(async () =>
        {
            var extractor = new AssetSchemaExtractor(new IndexSchemaPropertyCollector());

            var result = await extractor.ExtractAsync(
                new[] { typeof(IndexCatalogTestAsset) },
                CancellationToken.None);

            Assert.That(result.Entries.Count, Is.EqualTo(0));
            Assert.That(result.ReferencedTypes.Count, Is.EqualTo(0));
        });

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
        public IEnumerator Build_WhenSchemaExtractorThrows_ReturnsFailureResult () => UniTask.ToCoroutine(async () =>
        {
            var builder = CreateBuilder(
                new ThrowingComponentSchemaExtractor(),
                new EmptyAssetSchemaExtractor(),
                new SuccessIndexInputFingerprintCalculator());

            var result = await builder.BuildAsync(ResolveProjectRootPath(), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.TypesCatalog, Is.Null);
            Assert.That(result.SchemasCatalog, Is.Null);
            Assert.That(result.InputsManifest, Is.Null);
            Assert.That(result.ErrorMessage, Does.Contain("Failed to build index catalogs."));
            Assert.That(result.ErrorMessage, Does.Contain("component-extractor-failure"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator InputFingerprintCalculator_WhenRequiredInputsExist_ReturnsHashes () => UniTask.ToCoroutine(async () =>
        {
            var calculator = new FileSystemIndexInputFingerprintCalculator();
            var projectRootPath = ResolveProjectRootPath();

            var snapshot = await calculator.TryComputeAsync(projectRootPath, CancellationToken.None);

            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.ScriptAssembliesHash, Is.Not.Empty);
            Assert.That(snapshot.PackagesManifestHash, Is.Not.Empty);
            Assert.That(snapshot.PackagesLockHash, Is.Not.Empty);
            Assert.That(snapshot.AssemblyDefinitionHash, Is.Not.Empty);
            Assert.That(snapshot.CombinedHash, Is.Not.Empty);
        });

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
            var scenePath = scope.CreateScenePath(nameof(IndexCatalogBuilderTests));
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
            var response = await builder.BuildAsync(scenePath, cancellationToken: CancellationToken.None);
            var roots = response.Roots;
            var activeSceneAfterBuild = SceneManager.GetActiveScene();
            var resolvedSceneAfterBuild = SceneManager.GetSceneByPath(scenePath);

            Assert.That(response.ScenePath, Is.EqualTo(scenePath));
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
            var scenePath = scope.CreateScenePath(nameof(IndexCatalogBuilderTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("PersistedRoot");
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            var activeSceneBeforeBuild = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var activeSceneHandleBeforeBuild = activeSceneBeforeBuild.handle;
            var loadedSceneCountBeforeBuild = SceneManager.sceneCount;
            var builder = new SceneTreeLiteSnapshotBuilder();

            var exception = await AsyncExceptionCapture.CaptureAsync<ArgumentException>(async () =>
            {
                await builder.BuildAsync(scenePath, loadedSceneOnly: true, cancellationToken: CancellationToken.None).AsUniTask();
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
            var scenePath = scope.CreateScenePath(nameof(IndexCatalogBuilderTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("SavedRoot");
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            _ = new GameObject("UnsavedRoot");
            EditorSceneManager.MarkSceneDirty(scene);

            var builder = new SceneTreeLiteSnapshotBuilder();
            var response = await builder.BuildAsync(scenePath, cancellationToken: CancellationToken.None);
            var roots = response.Roots!;

            Assert.That(response.SourceState.Kind, Is.EqualTo(SceneTreeSourceStateKind.LoadedScene));
            Assert.That(response.SourceState.IsDirty, Is.True);
            Assert.That(roots.Select(static root => root.Name), Is.EquivalentTo(new[] { "SavedRoot", "UnsavedRoot" }));
        });

        private static string ResolveProjectRootPath ()
        {
            return UnityProjectPathResolver.ResolveProjectRootPath();
        }

        private static IndexCatalogBuilder CreateBuilder (
            IComponentSchemaExtractor componentSchemaExtractor,
            IAssetSchemaExtractor assetSchemaExtractor,
            IIndexInputFingerprintCalculator inputFingerprintCalculator)
        {
            return new IndexCatalogBuilder(
                componentSchemaExtractor,
                assetSchemaExtractor,
                inputFingerprintCalculator,
                new FixedProjectTypeCatalogSource(),
                new IndexTypeCatalogComposer());
        }

        private sealed class SuccessComponentSchemaExtractor : IComponentSchemaExtractor
        {
            public ValueTask<IndexSchemaExtractionResult> ExtractAsync (
                IReadOnlyList<Type> componentTypes,
                CancellationToken cancellationToken = default)
            {
                var componentTypeId = IndexTypeIdFormatter.Format(typeof(BoxCollider));
                var entry = new IndexSchemaEntryJsonContract(
                    SchemaKey: $"{"comp"}:{componentTypeId}",
                    Kind: "comp",
                    TypeId: componentTypeId,
                    DisplayName: nameof(BoxCollider),
                    Properties: new[]
                    {
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "m_Enabled",
                            PropertyType: "boolean",
                            DeclaredTypeId: IndexTypeIdFormatter.Format(typeof(bool)),
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: false),
                    });
                return new ValueTask<IndexSchemaExtractionResult>(new IndexSchemaExtractionResult(
                    new[] { entry },
                    new HashSet<Type> { typeof(BoxCollider), typeof(bool) }));
            }
        }

        private sealed class SuccessAssetSchemaExtractor : IAssetSchemaExtractor
        {
            public ValueTask<IndexSchemaExtractionResult> ExtractAsync (
                IReadOnlyList<Type> assetTypes,
                CancellationToken cancellationToken = default)
            {
                var assetTypeId = IndexTypeIdFormatter.Format(typeof(IndexCatalogTestAsset));
                var entry = new IndexSchemaEntryJsonContract(
                    SchemaKey: $"{"asset"}:{assetTypeId}",
                    Kind: "asset",
                    TypeId: assetTypeId,
                    DisplayName: nameof(IndexCatalogTestAsset),
                    Properties: new[]
                    {
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "speed",
                            PropertyType: "float",
                            DeclaredTypeId: IndexTypeIdFormatter.Format(typeof(float)),
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: false),
                    });
                return new ValueTask<IndexSchemaExtractionResult>(new IndexSchemaExtractionResult(
                    new[] { entry },
                    new HashSet<Type> { typeof(IndexCatalogTestAsset), typeof(float) }));
            }
        }

        private sealed class ThrowingPropertyCollector : IIndexSchemaPropertyCollector
        {
            public IndexSchemaPropertyCollectionResult Collect (
                Type rootType,
                SerializedObject serializedObject)
            {
                throw new InvalidOperationException("test-collector-failure");
            }
        }

        private sealed class ThrowingComponentSchemaExtractor : IComponentSchemaExtractor
        {
            public ValueTask<IndexSchemaExtractionResult> ExtractAsync (
                IReadOnlyList<Type> componentTypes,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("component-extractor-failure");
            }
        }

        private sealed class EmptyAssetSchemaExtractor : IAssetSchemaExtractor
        {
            public ValueTask<IndexSchemaExtractionResult> ExtractAsync (
                IReadOnlyList<Type> assetTypes,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<IndexSchemaExtractionResult>(IndexSchemaExtractionResult.Empty());
            }
        }

        private sealed class SuccessIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
        {
            public ValueTask<IndexCoreInputHashSnapshot?> TryComputeCoreAsync (
                string projectRootPath,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<IndexCoreInputHashSnapshot?>(
                    new IndexCoreInputHashSnapshot(
                        ScriptAssembliesHash: "script-hash",
                        PackagesManifestHash: "manifest-hash",
                        PackagesLockHash: "lock-hash",
                        AssemblyDefinitionHash: "asm-hash",
                        CombinedHash: "combined-hash"));
            }

            public ValueTask<IndexInputHashSnapshot?> TryComputeAsync (
                string projectRootPath,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<IndexInputHashSnapshot?>(
                    new IndexInputHashSnapshot(
                        ScriptAssembliesHash: "script-hash",
                        PackagesManifestHash: "manifest-hash",
                        PackagesLockHash: "lock-hash",
                        AssemblyDefinitionHash: "asm-hash",
                        AssetsContentHash: "assets-hash",
                        AssetSearchHash: "asset-search-hash",
                        GuidPathHash: "guid-path-hash",
                        CombinedHash: "combined-hash"));
            }
        }

        private sealed class FixedProjectTypeCatalogSource : IIndexProjectTypeCatalogSource
        {
            public IndexProjectTypeCatalog Resolve ()
            {
                return new IndexProjectTypeCatalog(
                    new[] { typeof(BoxCollider), typeof(IndexCatalogTestComponent) },
                    new[] { typeof(IndexCatalogTestAsset) },
                    new[] { typeof(IndexCatalogTestSerializeReferenceCandidate) });
            }
        }
    }
}
