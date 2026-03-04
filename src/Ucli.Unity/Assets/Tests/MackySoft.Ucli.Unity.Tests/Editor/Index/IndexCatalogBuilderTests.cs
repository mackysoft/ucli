using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class IndexCatalogBuilderTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task Build_WhenRequiredInputsExist_ReturnsCatalogsAndContainsExpectedEntries ()
        {
            var projectRootPath = ResolveProjectRootPath();
            var builder = CreateBuilder();

            var result = await builder.Build(projectRootPath, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ErrorMessage, Is.Null);
            Assert.That(result.TypesCatalog, Is.Not.Null);
            Assert.That(result.SchemasCatalog, Is.Not.Null);
            Assert.That(result.InputsManifest, Is.Not.Null);
            Assert.That(result.TypesCatalog!.Entries, Is.Not.Null);
            Assert.That(result.SchemasCatalog!.Entries, Is.Not.Null);

            var componentTypeId = IndexTypeIdFormatter.Format(typeof(IndexCatalogTestComponent));
            var assetTypeId = IndexTypeIdFormatter.Format(typeof(IndexCatalogTestAsset));
            var candidateTypeId = IndexTypeIdFormatter.Format(typeof(IndexCatalogTestSerializeReferenceCandidate));

            var componentTypeEntry = result.TypesCatalog.Entries!.SingleOrDefault(entry => entry.TypeId == componentTypeId);
            Assert.That(componentTypeEntry, Is.Not.Null);
            Assert.That(componentTypeEntry!.Flags, Is.Not.Null);
            Assert.That(componentTypeEntry.Flags!.IsComponent, Is.True);

            var assetTypeEntry = result.TypesCatalog.Entries!.SingleOrDefault(entry => entry.TypeId == assetTypeId);
            Assert.That(assetTypeEntry, Is.Not.Null);
            Assert.That(assetTypeEntry!.Flags, Is.Not.Null);
            Assert.That(assetTypeEntry.Flags!.IsScriptableObject, Is.True);

            var candidateTypeEntry = result.TypesCatalog.Entries!.SingleOrDefault(entry => entry.TypeId == candidateTypeId);
            Assert.That(candidateTypeEntry, Is.Not.Null);
            Assert.That(candidateTypeEntry!.Flags, Is.Not.Null);
            Assert.That(candidateTypeEntry.Flags!.IsSerializeReferenceCandidate, Is.True);

            var componentSchema = result.SchemasCatalog.Entries!.SingleOrDefault(entry =>
                entry.Kind == IndexSchemaKindValues.Comp
                && entry.TypeId == componentTypeId);
            Assert.That(componentSchema, Is.Not.Null);
            Assert.That(componentSchema!.SchemaKey, Is.EqualTo($"{IndexSchemaKindValues.Comp}:{componentTypeId}"));
            Assert.That(componentSchema.Properties, Is.Not.Null);

            var integerProperty = componentSchema.Properties!.SingleOrDefault(property => property.Path == "integerValue");
            Assert.That(integerProperty, Is.Not.Null);
            Assert.That(integerProperty!.PropertyType, Is.EqualTo(IndexPropertyTypeValues.Integer));
            Assert.That(integerProperty.IsArray, Is.False);

            var listProperty = componentSchema.Properties!.SingleOrDefault(property => property.Path == "items");
            Assert.That(listProperty, Is.Not.Null);
            Assert.That(listProperty!.IsArray, Is.True);
            Assert.That(listProperty.ElementTypeId, Is.Not.Null.And.Not.Empty);

            var readOnlyProperty = componentSchema.Properties!.SingleOrDefault(property => property.Path == "m_Script");
            Assert.That(readOnlyProperty, Is.Not.Null);
            Assert.That(readOnlyProperty!.IsReadOnly, Is.True);

            var assetSchema = result.SchemasCatalog.Entries!.SingleOrDefault(entry =>
                entry.Kind == IndexSchemaKindValues.Asset
                && entry.TypeId == assetTypeId);
            Assert.That(assetSchema, Is.Not.Null);
            Assert.That(assetSchema!.SchemaKey, Is.EqualTo($"{IndexSchemaKindValues.Asset}:{assetTypeId}"));
        }

        [Test]
        [Category("Size.Small")]
        public void Extractors_WhenCalledDirectly_SeparateCompAndAssetKinds ()
        {
            var propertyCollector = new IndexSchemaPropertyCollector();
            var componentExtractor = new ComponentSchemaExtractor(propertyCollector);
            var assetExtractor = new AssetSchemaExtractor(propertyCollector);

            var componentResult = componentExtractor.Extract(new[] { typeof(IndexCatalogTestComponent) });
            var assetResult = assetExtractor.Extract(new[] { typeof(IndexCatalogTestAsset) });

            Assert.That(componentResult.Entries.Count, Is.EqualTo(1));
            Assert.That(componentResult.Entries[0].Kind, Is.EqualTo(IndexSchemaKindValues.Comp));
            Assert.That(componentResult.Entries[0].SchemaKey, Does.StartWith($"{IndexSchemaKindValues.Comp}:"));

            Assert.That(assetResult.Entries.Count, Is.EqualTo(1));
            Assert.That(assetResult.Entries[0].Kind, Is.EqualTo(IndexSchemaKindValues.Asset));
            Assert.That(assetResult.Entries[0].SchemaKey, Does.StartWith($"{IndexSchemaKindValues.Asset}:"));
        }

        [Test]
        [Category("Size.Small")]
        public async Task InputFingerprintCalculator_WhenRequiredInputsExist_ReturnsHashes ()
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
        }

        [Test]
        [Category("Size.Small")]
        public async Task FileIndexCatalogWriter_WhenWriteSucceeds_CreatesExpectedCatalogPaths ()
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
        }

        private static IIndexCatalogBuilder CreateBuilder ()
        {
            var propertyCollector = new IndexSchemaPropertyCollector();
            return new IndexCatalogBuilder(
                new ComponentSchemaExtractor(propertyCollector),
                new AssetSchemaExtractor(propertyCollector),
                new FileSystemIndexInputFingerprintCalculator());
        }

        private static string ResolveProjectRootPath ()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
