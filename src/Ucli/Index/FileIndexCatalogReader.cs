using System.Text.Json;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Index;

/// <summary> Provides filesystem-backed access to index catalog contract files. </summary>
internal sealed class FileIndexCatalogReader : IIndexCatalogReader
{
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