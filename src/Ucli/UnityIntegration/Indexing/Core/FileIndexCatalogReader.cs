using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Provides filesystem-backed access to index catalog contract files. </summary>
internal sealed class FileIndexCatalogReader : IIndexCatalogReader
{
    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    public ValueTask<IndexAccessResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            storageRoot,
            projectFingerprint,
            UcliStoragePathResolver.ResolveOpsCatalogPath,
            static json => IndexOpsCatalogJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidOpsCatalog(contract),
            "ops.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>types.catalog.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    public ValueTask<IndexAccessResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            storageRoot,
            projectFingerprint,
            UcliStoragePathResolver.ResolveTypesCatalogPath,
            static json => IndexTypesCatalogJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidTypesCatalog(contract),
            "types.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>schemas.catalog.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    public ValueTask<IndexAccessResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            storageRoot,
            projectFingerprint,
            UcliStoragePathResolver.ResolveSchemasCatalogPath,
            static json => IndexSchemasCatalogJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidSchemasCatalog(contract),
            "schemas.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public ValueTask<IndexAccessResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            storageRoot,
            projectFingerprint,
            UcliStoragePathResolver.ResolveAssetSearchLookupPath,
            static json => IndexAssetSearchLookupJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidAssetSearchLookup(contract),
            "lookups/asset-search.lookup.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public ValueTask<IndexAccessResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            storageRoot,
            projectFingerprint,
            UcliStoragePathResolver.ResolveGuidPathLookupPath,
            static json => IndexGuidPathLookupJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidGuidPathLookup(contract),
            "lookups/guid-path.lookup.json",
            cancellationToken);
    }

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="scenePath"> The project-relative scene path represented by the lookup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public async ValueTask<IndexAccessResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
        string storageRoot,
        string projectFingerprint,
        string scenePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return IndexAccessResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                IpcErrorCodes.InvalidArgument,
                "Scene path must not be empty.");
        }

        string contractPath;
        try
        {
            contractPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(storageRoot, projectFingerprint, scenePath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return IndexAccessResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                IpcErrorCodes.InvalidArgument,
                $"Index path is invalid. {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return IndexAccessResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                IpcErrorCodes.InvalidArgument,
                ex.Message);
        }

        var result = await ReadContract(
                contractPath,
                static json => IndexSceneTreeLiteLookupJsonContractSerializer.Deserialize(json),
                static contract => IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract),
                "lookups/scene-tree-lite/*.lookup.json",
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        var normalizedScenePath = PathStringNormalizer.ToSlashSeparated(scenePath);
        if (!string.Equals(result.Value!.ScenePath, normalizedScenePath, StringComparison.Ordinal))
        {
            return IndexAccessResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                IpcErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'lookups/scene-tree-lite/*.lookup.json' is malformed. scenePath does not match the requested scene path.");
        }

        return result;
    }

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to manifest-read result. </returns>
    public ValueTask<IndexAccessResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            storageRoot,
            projectFingerprint,
            UcliStoragePathResolver.ResolveIndexInputsManifestPath,
            static json => IndexInputsManifestJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidInputsManifest(contract),
            "inputs/manifest.json",
            cancellationToken);
    }

    private static async ValueTask<IndexAccessResult<TContract>> ReadContract<TContract> (
        string storageRoot,
        string projectFingerprint,
        Func<string, string, string> pathResolver,
        Func<string, TContract?> deserialize,
        Func<TContract, bool> validator,
        string contractName,
        CancellationToken cancellationToken)
        where TContract : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.InvalidArgument,
                "Storage root path must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.InvalidArgument,
                "Project fingerprint must not be empty.");
        }

        string contractPath;
        try
        {
            contractPath = pathResolver(storageRoot, projectFingerprint);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.InvalidArgument,
                $"Index path is invalid. {ex.Message}");
        }

        return await ReadContract(
                contractPath,
                deserialize,
                validator,
                contractName,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<IndexAccessResult<TContract>> ReadContract<TContract> (
        string contractPath,
        Func<string, TContract?> deserialize,
        Func<TContract, bool> validator,
        string contractName,
        CancellationToken cancellationToken)
        where TContract : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(contractPath))
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                $"Index contract file was not found: {contractName}.");
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(contractPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.InvalidArgument,
                $"Index path is invalid: {contractPath}. {ex.Message}");
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                $"Failed to read index contract file '{contractName}'. {ex.Message}");
        }

        TContract? contract;
        try
        {
            contract = deserialize(json);
        }
        catch (ArgumentException ex)
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }
        catch (JsonException ex)
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }

        if (contract == null || !validator(contract))
        {
            return IndexAccessResult<TContract>.Failure(
                IpcErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed.");
        }

        return IndexAccessResult<TContract>.Success(contract);
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException;
    }
}
