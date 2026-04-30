using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Persists generated index contracts to filesystem-backed storage paths. </summary>
internal sealed class FileIndexCatalogWriter : IIndexCatalogWriter
{
    /// <summary> Writes generated index contracts to one storage root and project fingerprint directory. </summary>
    /// <param name="storageRootPath"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="typesCatalog"> The generated <c>types.catalog.json</c> contract. </param>
    /// <param name="schemasCatalog"> The generated <c>schemas.catalog.json</c> contract. </param>
    /// <param name="inputsManifest"> The generated <c>inputs/manifest.json</c> contract. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
    /// <returns> The write result. </returns>
    /// <exception cref="ArgumentException"> Thrown when path arguments are <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when any contract argument is <see langword="null" />. </exception>
    public async ValueTask<IndexCatalogWriteResult> Write (
        string storageRootPath,
        string projectFingerprint,
        IndexTypesCatalogJsonContract typesCatalog,
        IndexSchemasCatalogJsonContract schemasCatalog,
        IndexInputsManifestJsonContract inputsManifest,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(storageRootPath))
        {
            throw new ArgumentException("Storage root path must not be empty.", nameof(storageRootPath));
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        if (typesCatalog == null)
        {
            throw new ArgumentNullException(nameof(typesCatalog));
        }

        if (schemasCatalog == null)
        {
            throw new ArgumentNullException(nameof(schemasCatalog));
        }

        if (inputsManifest == null)
        {
            throw new ArgumentNullException(nameof(inputsManifest));
        }

        try
        {
            var typesCatalogPath = UcliStoragePathResolver.ResolveTypesCatalogPath(storageRootPath, projectFingerprint);
            var schemasCatalogPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(storageRootPath, projectFingerprint);
            var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRootPath, projectFingerprint);

            EnsureParentDirectory(typesCatalogPath);
            EnsureParentDirectory(schemasCatalogPath);
            EnsureParentDirectory(inputsManifestPath);

            await FileUtilities.WriteAllTextAtomically(
                    typesCatalogPath,
                    IndexTypesCatalogJsonContractSerializer.Serialize(typesCatalog) + Environment.NewLine,
                    cancellationToken)
                .ConfigureAwait(false);
            await FileUtilities.WriteAllTextAtomically(
                    schemasCatalogPath,
                    IndexSchemasCatalogJsonContractSerializer.Serialize(schemasCatalog) + Environment.NewLine,
                    cancellationToken)
                .ConfigureAwait(false);
            await FileUtilities.WriteAllTextAtomically(
                    inputsManifestPath,
                    IndexInputsManifestJsonContractSerializer.Serialize(inputsManifest) + Environment.NewLine,
                    cancellationToken)
                .ConfigureAwait(false);
            return IndexCatalogWriteResult.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return IndexCatalogWriteResult.Failure($"Failed to write index catalogs. {exception.Message}");
        }
    }

    private static void EnsureParentDirectory (string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be resolved. {filePath}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
    }
}
