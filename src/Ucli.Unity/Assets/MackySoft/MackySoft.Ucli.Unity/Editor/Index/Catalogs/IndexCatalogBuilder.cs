using MackySoft.Ucli.Infrastructure.Index;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds types/schema catalogs and input manifest contracts for read-index persistence. </summary>
    internal sealed class IndexCatalogBuilder : IIndexCatalogBuilder
    {
        private const int ContractSchemaVersion = 1;

        private readonly IComponentSchemaExtractor componentSchemaExtractor;
        private readonly IAssetSchemaExtractor assetSchemaExtractor;
        private readonly IIndexInputFingerprintCalculator inputFingerprintCalculator;
        private readonly IIndexProjectTypeCatalogSource projectTypeCatalogSource;
        private readonly IIndexTypeCatalogComposer typeCatalogComposer;

        /// <summary> Initializes a new instance of the <see cref="IndexCatalogBuilder" /> class. </summary>
        /// <param name="componentSchemaExtractor"> The component schema extractor dependency. </param>
        /// <param name="assetSchemaExtractor"> The asset schema extractor dependency. </param>
        /// <param name="inputFingerprintCalculator"> The input fingerprint calculator dependency. </param>
        /// <param name="projectTypeCatalogSource"> The project type-catalog source dependency. </param>
        /// <param name="typeCatalogComposer"> The type catalog composer dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public IndexCatalogBuilder (
            IComponentSchemaExtractor componentSchemaExtractor,
            IAssetSchemaExtractor assetSchemaExtractor,
            IIndexInputFingerprintCalculator inputFingerprintCalculator,
            IIndexProjectTypeCatalogSource projectTypeCatalogSource,
            IIndexTypeCatalogComposer typeCatalogComposer)
        {
            this.componentSchemaExtractor = componentSchemaExtractor ?? throw new ArgumentNullException(nameof(componentSchemaExtractor));
            this.assetSchemaExtractor = assetSchemaExtractor ?? throw new ArgumentNullException(nameof(assetSchemaExtractor));
            this.inputFingerprintCalculator = inputFingerprintCalculator ?? throw new ArgumentNullException(nameof(inputFingerprintCalculator));
            this.projectTypeCatalogSource = projectTypeCatalogSource ?? throw new ArgumentNullException(nameof(projectTypeCatalogSource));
            this.typeCatalogComposer = typeCatalogComposer ?? throw new ArgumentNullException(nameof(typeCatalogComposer));
        }

        /// <summary> Builds one full index catalog snapshot for one project root path. </summary>
        /// <param name="projectRootPath"> The Unity project root path. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The build result. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        public async ValueTask<IndexCatalogBuildResult> Build (
            string projectRootPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
            }

            try
            {
                var inputSnapshot = await inputFingerprintCalculator.TryCompute(projectRootPath, cancellationToken);
                if (inputSnapshot == null)
                {
                    return IndexCatalogBuildResult.Failure("Failed to compute index input snapshot.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var projectTypeCatalog = projectTypeCatalogSource.Resolve();

                cancellationToken.ThrowIfCancellationRequested();
                var componentSchemaResult = await componentSchemaExtractor.Extract(projectTypeCatalog.ComponentTypes, cancellationToken);
                var assetSchemaResult = await assetSchemaExtractor.Extract(projectTypeCatalog.AssetTypes, cancellationToken);
                var schemaEntries = IndexJsonOrderingPolicy.OrderSchemaEntries(
                    componentSchemaResult.Entries.Concat(assetSchemaResult.Entries));

                cancellationToken.ThrowIfCancellationRequested();
                var typeEntries = typeCatalogComposer.Compose(
                    projectTypeCatalog,
                    componentSchemaResult.ReferencedTypes,
                    assetSchemaResult.ReferencedTypes);

                cancellationToken.ThrowIfCancellationRequested();
                var generatedAtUtc = DateTimeOffset.UtcNow;
                var sourceInputsHash = inputSnapshot.CombinedHash;
                var typesCatalog = new IndexTypesCatalogJsonContract(
                    SchemaVersion: ContractSchemaVersion,
                    GeneratedAtUtc: generatedAtUtc,
                    SourceInputsHash: sourceInputsHash,
                    Entries: typeEntries);
                var schemasCatalog = new IndexSchemasCatalogJsonContract(
                    SchemaVersion: ContractSchemaVersion,
                    GeneratedAtUtc: generatedAtUtc,
                    SourceInputsHash: sourceInputsHash,
                    Entries: schemaEntries);
                var inputsManifest = new IndexInputsManifestJsonContract(
                    SchemaVersion: ContractSchemaVersion,
                    GeneratedAtUtc: generatedAtUtc,
                    ScriptAssembliesHash: inputSnapshot.ScriptAssembliesHash,
                    PackagesManifestHash: inputSnapshot.PackagesManifestHash,
                    PackagesLockHash: inputSnapshot.PackagesLockHash,
                    AssemblyDefinitionHash: inputSnapshot.AssemblyDefinitionHash,
                    AssetsContentHash: inputSnapshot.AssetsContentHash,
                    AssetSearchHash: inputSnapshot.AssetSearchHash,
                    GuidPathHash: inputSnapshot.GuidPathHash,
                    CombinedHash: inputSnapshot.CombinedHash);

                return IndexCatalogBuildResult.Success(typesCatalog, schemasCatalog, inputsManifest);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return IndexCatalogBuildResult.Failure($"Failed to build index catalogs. {exception.Message}");
            }
        }
    }
}
