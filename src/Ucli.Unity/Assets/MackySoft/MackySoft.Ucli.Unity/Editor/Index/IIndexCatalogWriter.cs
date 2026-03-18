using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Writes generated index contracts into persistent storage. </summary>
    internal interface IIndexCatalogWriter
    {
        /// <summary> Writes generated index contracts to one storage root and project fingerprint directory. </summary>
        /// <param name="storageRootPath"> The storage-root path. </param>
        /// <param name="projectFingerprint"> The project fingerprint value. </param>
        /// <param name="typesCatalog"> The generated <c>types.catalog.json</c> contract. </param>
        /// <param name="schemasCatalog"> The generated <c>schemas.catalog.json</c> contract. </param>
        /// <param name="inputsManifest"> The generated <c>inputs/manifest.json</c> contract. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The write result. </returns>
        ValueTask<IndexCatalogWriteResult> Write (
            string storageRootPath,
            string projectFingerprint,
            IndexTypesCatalogJsonContract typesCatalog,
            IndexSchemasCatalogJsonContract schemasCatalog,
            IndexInputsManifestJsonContract inputsManifest,
            CancellationToken cancellationToken = default);
    }
}