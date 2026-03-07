using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class IndexCatalogBuilderTests
    {
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

            var result = await builder.Build(ResolveProjectRootPath(), CancellationToken.None);

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
                entry.Kind == IndexSchemaKindValues.Comp
                && entry.TypeId == componentTypeId);
            Assert.That(componentSchema, Is.Not.Null);
            Assert.That(componentSchema!.SchemaKey, Is.EqualTo($"{IndexSchemaKindValues.Comp}:{componentTypeId}"));
            var enabledProperty = componentSchema.Properties!.SingleOrDefault(property => property.Path == "m_Enabled");
            Assert.That(enabledProperty, Is.Not.Null);
            Assert.That(enabledProperty!.PropertyType, Is.EqualTo(IndexPropertyTypeValues.Boolean));
            Assert.That(enabledProperty.DeclaredTypeId, Is.EqualTo(IndexTypeIdFormatter.Format(typeof(bool))));

            var assetSchema = result.SchemasCatalog.Entries!.SingleOrDefault(entry =>
                entry.Kind == IndexSchemaKindValues.Asset
                && entry.TypeId == assetTypeId);
            Assert.That(assetSchema, Is.Not.Null);
            Assert.That(assetSchema!.SchemaKey, Is.EqualTo($"{IndexSchemaKindValues.Asset}:{assetTypeId}"));
            var speedProperty = assetSchema.Properties!.SingleOrDefault(property => property.Path == "speed");
            Assert.That(speedProperty, Is.Not.Null);
            Assert.That(speedProperty!.PropertyType, Is.EqualTo(IndexPropertyTypeValues.Float));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Build_WhenExtractorsReturnBothKinds_MergesAndOrdersSchemas () => UniTask.ToCoroutine(async () =>
        {
            var builder = CreateBuilder(
                new SuccessComponentSchemaExtractor(),
                new SuccessAssetSchemaExtractor(),
                new SuccessIndexInputFingerprintCalculator());

            var result = await builder.Build(ResolveProjectRootPath(), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.SchemasCatalog, Is.Not.Null);
            Assert.That(result.SchemasCatalog!.Entries, Is.Not.Null);
            Assert.That(result.SchemasCatalog.Entries!.Count, Is.EqualTo(2));
            Assert.That(result.SchemasCatalog.Entries[0].Kind, Is.EqualTo(IndexSchemaKindValues.Asset));
            Assert.That(result.SchemasCatalog.Entries[1].Kind, Is.EqualTo(IndexSchemaKindValues.Comp));
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

            Assert.That(literal, Is.EqualTo(IndexPropertyTypeValues.LayerMask));
            Assert.That(
                IndexPropertyTypeCodec.TryParse(literal, out var parsedType),
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
                await componentExtractor.Extract(
                    new[] { typeof(BoxCollider) },
                    cts.Token);
            });

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await assetExtractor.Extract(
                    new[] { typeof(GUISkin) },
                    cts.Token);
            });
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Extractors_WhenCollectorThrows_PropagateException () => UniTask.ToCoroutine(async () =>
        {
            var componentExtractor = new ComponentSchemaExtractor(new ThrowingPropertyCollector());
            var assetExtractor = new AssetSchemaExtractor(new ThrowingPropertyCollector());

            async UniTask RunComponentExtract ()
            {
                await componentExtractor.Extract(
                    new[] { typeof(BoxCollider) },
                    CancellationToken.None);
            }

            async UniTask RunAssetExtract ()
            {
                await assetExtractor.Extract(
                    new[] { typeof(GUISkin) },
                    CancellationToken.None);
            }

            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(RunComponentExtract);
            await AsyncExceptionCapture.CaptureAsync<InvalidOperationException>(RunAssetExtract);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Extractors_WhenAssetTypeIsEditorAssembly_SkipsExtraction () => UniTask.ToCoroutine(async () =>
        {
            var extractor = new AssetSchemaExtractor(new IndexSchemaPropertyCollector());

            var result = await extractor.Extract(
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

            var result = await builder.Build(ResolveProjectRootPath(), CancellationToken.None);

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

            var snapshot = await calculator.TryCompute(projectRootPath, CancellationToken.None);

            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.ScriptAssembliesHash, Is.Not.Empty);
            Assert.That(snapshot.PackagesManifestHash, Is.Not.Empty);
            Assert.That(snapshot.PackagesLockHash, Is.Not.Empty);
            Assert.That(snapshot.AssemblyDefinitionHash, Is.Not.Empty);
            Assert.That(snapshot.CombinedHash, Is.Not.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator FileIndexCatalogWriter_WhenWriteSucceeds_CreatesExpectedCatalogPaths () => UniTask.ToCoroutine(async () =>
        {
            var writer = new FileIndexCatalogWriter();
            var generatedAtUtc = DateTimeOffset.Parse("2026-03-04T00:00:00+00:00");
            var typesCatalog = new IndexTypesCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: generatedAtUtc,
                SourceInputsHash: "combined-hash",
                Entries: new[]
                {
                    new IndexTypeEntryJsonContract(
                        TypeId: IndexTypeIdFormatter.Format(typeof(IndexCatalogTestComponent)),
                        DisplayName: nameof(IndexCatalogTestComponent),
                        Namespace: typeof(IndexCatalogTestComponent).Namespace,
                        AssemblyName: typeof(IndexCatalogTestComponent).Assembly.GetName().Name,
                        BaseTypeId: IndexTypeIdFormatter.Format(typeof(MonoBehaviour)),
                        Flags: new IndexTypeFlagsJsonContract(
                            IsAbstract: false,
                            IsGenericDefinition: false,
                            IsUnityObject: true,
                            IsComponent: true,
                            IsScriptableObject: false,
                            IsSerializeReferenceCandidate: false)),
                });
            var schemasCatalog = new IndexSchemasCatalogJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: generatedAtUtc,
                SourceInputsHash: "combined-hash",
                Entries: new[]
                {
                    new IndexSchemaEntryJsonContract(
                        SchemaKey: $"comp:{IndexTypeIdFormatter.Format(typeof(IndexCatalogTestComponent))}",
                        Kind: IndexSchemaKindValues.Comp,
                        TypeId: IndexTypeIdFormatter.Format(typeof(IndexCatalogTestComponent)),
                        DisplayName: nameof(IndexCatalogTestComponent),
                        Properties: new[]
                        {
                            new IndexSchemaPropertyEntryJsonContract(
                                Path: "integerValue",
                                PropertyType: IndexPropertyTypeValues.Integer,
                                DeclaredTypeId: IndexTypeIdFormatter.Format(typeof(int)),
                                IsArray: false,
                                ElementTypeId: null,
                                IsReadOnly: false),
                        }),
                });
            var inputsManifest = new IndexInputsManifestJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: generatedAtUtc,
                ScriptAssembliesHash: "script-hash",
                PackagesManifestHash: "manifest-hash",
                PackagesLockHash: "lock-hash",
                AssemblyDefinitionHash: "asm-hash",
                CombinedHash: "combined-hash");
            var storageRootPath = Path.Combine(Path.GetTempPath(), $"ucli-index-writer-tests-{Guid.NewGuid():N}");
            const string projectFingerprint = "writer-fingerprint";
            try
            {
                var result = await writer.Write(
                    storageRootPath,
                    projectFingerprint,
                    typesCatalog,
                    schemasCatalog,
                    inputsManifest,
                    CancellationToken.None);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.ErrorMessage, Is.Null);

                var typesCatalogPath = UcliStoragePathResolver.ResolveTypesCatalogPath(storageRootPath, projectFingerprint);
                var schemasCatalogPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(storageRootPath, projectFingerprint);
                var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRootPath, projectFingerprint);
                Assert.That(File.Exists(typesCatalogPath), Is.True);
                Assert.That(File.Exists(schemasCatalogPath), Is.True);
                Assert.That(File.Exists(inputsManifestPath), Is.True);
            }
            finally
            {
                if (Directory.Exists(storageRootPath))
                {
                    Directory.Delete(storageRootPath, recursive: true);
                }
            }
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
            public ValueTask<IndexSchemaExtractionResult> Extract (
                IReadOnlyList<Type> componentTypes,
                CancellationToken cancellationToken = default)
            {
                var componentTypeId = IndexTypeIdFormatter.Format(typeof(BoxCollider));
                var entry = new IndexSchemaEntryJsonContract(
                    SchemaKey: $"{IndexSchemaKindValues.Comp}:{componentTypeId}",
                    Kind: IndexSchemaKindValues.Comp,
                    TypeId: componentTypeId,
                    DisplayName: nameof(BoxCollider),
                    Properties: new[]
                    {
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "m_Enabled",
                            PropertyType: IndexPropertyTypeValues.Boolean,
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
            public ValueTask<IndexSchemaExtractionResult> Extract (
                IReadOnlyList<Type> assetTypes,
                CancellationToken cancellationToken = default)
            {
                var assetTypeId = IndexTypeIdFormatter.Format(typeof(IndexCatalogTestAsset));
                var entry = new IndexSchemaEntryJsonContract(
                    SchemaKey: $"{IndexSchemaKindValues.Asset}:{assetTypeId}",
                    Kind: IndexSchemaKindValues.Asset,
                    TypeId: assetTypeId,
                    DisplayName: nameof(IndexCatalogTestAsset),
                    Properties: new[]
                    {
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "speed",
                            PropertyType: IndexPropertyTypeValues.Float,
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
            public ValueTask<IndexSchemaExtractionResult> Extract (
                IReadOnlyList<Type> componentTypes,
                CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("component-extractor-failure");
            }
        }

        private sealed class EmptyAssetSchemaExtractor : IAssetSchemaExtractor
        {
            public ValueTask<IndexSchemaExtractionResult> Extract (
                IReadOnlyList<Type> assetTypes,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<IndexSchemaExtractionResult>(IndexSchemaExtractionResult.Empty());
            }
        }

        private sealed class SuccessIndexInputFingerprintCalculator : IIndexInputFingerprintCalculator
        {
            public ValueTask<IndexInputHashSnapshot?> TryCompute (
                string projectRootPath,
                CancellationToken cancellationToken = default)
            {
                return new ValueTask<IndexInputHashSnapshot?>(
                    new IndexInputHashSnapshot(
                        ScriptAssembliesHash: "script-hash",
                        PackagesManifestHash: "manifest-hash",
                        PackagesLockHash: "lock-hash",
                        AssemblyDefinitionHash: "asm-hash",
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
