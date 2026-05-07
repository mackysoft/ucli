using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Provides filesystem-backed access to read-index artifact contract files. </summary>
internal sealed class FileReadIndexArtifactReader : IReadIndexArtifactReader
{
    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalog (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            unityProject,
            UcliStoragePathResolver.ResolveOpsCatalogPath,
            static json => IndexOpsCatalogJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidOpsCatalog(contract),
            "ops.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>types.catalog.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalog (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            unityProject,
            UcliStoragePathResolver.ResolveTypesCatalogPath,
            static json => IndexTypesCatalogJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidTypesCatalog(contract),
            "types.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>schemas.catalog.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalog (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            unityProject,
            UcliStoragePathResolver.ResolveSchemasCatalogPath,
            static json => IndexSchemasCatalogJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidSchemasCatalog(contract),
            "schemas.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookup (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            unityProject,
            UcliStoragePathResolver.ResolveAssetSearchLookupPath,
            static json => IndexAssetSearchLookupJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidAssetSearchLookup(contract),
            "lookups/asset-search.lookup.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookup (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            unityProject,
            UcliStoragePathResolver.ResolveGuidPathLookupPath,
            static json => IndexGuidPathLookupJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidGuidPathLookup(contract),
            "lookups/guid-path.lookup.json",
            cancellationToken);
    }

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="scenePath"> The project-relative scene path represented by the lookup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public async ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookup (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);

        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                "Scene path must not be empty.");
        }

        string contractPath;
        try
        {
            contractPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                scenePath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid. {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
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
            return ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'lookups/scene-tree-lite/*.lookup.json' is malformed. scenePath does not match the requested scene path.");
        }

        return result;
    }

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to manifest-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifest (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContract(
            unityProject,
            UcliStoragePathResolver.ResolveIndexInputsManifestPath,
            static json => IndexInputsManifestJsonContractSerializer.Deserialize(json),
            static contract => IndexCatalogContractValidator.IsValidInputsManifest(contract),
            "inputs/manifest.json",
            cancellationToken);
    }

    private static async ValueTask<ReadIndexArtifactReadResult<TContract>> ReadContract<TContract> (
        ResolvedUnityProjectContext unityProject,
        Func<string, string, string> pathResolver,
        Func<string, TContract?> deserialize,
        Func<TContract, bool> validator,
        string contractName,
        CancellationToken cancellationToken)
        where TContract : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        if (string.IsNullOrWhiteSpace(unityProject.RepositoryRoot))
        {
            return ReadIndexArtifactReadResult<TContract>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                "Storage root path must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(unityProject.ProjectFingerprint))
        {
            return ReadIndexArtifactReadResult<TContract>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                "Project fingerprint must not be empty.");
        }

        string contractPath;
        try
        {
            contractPath = pathResolver(unityProject.RepositoryRoot, unityProject.ProjectFingerprint);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return ReadIndexArtifactReadResult<TContract>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
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

    private static async ValueTask<ReadIndexArtifactReadResult<TContract>> ReadContract<TContract> (
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
            return ReadIndexArtifactReadResult<TContract>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
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
            return ReadIndexArtifactReadResult<TContract>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid: {contractPath}. {ex.Message}");
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return ReadIndexArtifactReadResult<TContract>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Failed to read index contract file '{contractName}'. {ex.Message}");
        }

        TContract? contract;
        try
        {
            contract = deserialize(json);
        }
        catch (ArgumentException ex)
        {
            return ReadIndexArtifactReadResult<TContract>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }
        catch (JsonException ex)
        {
            return ReadIndexArtifactReadResult<TContract>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }

        if (contract == null || !validator(contract))
        {
            return ReadIndexArtifactReadResult<TContract>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed.");
        }

        return ReadIndexArtifactReadResult<TContract>.Success(contract);
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException;
    }
}
