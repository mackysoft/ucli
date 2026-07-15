using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
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
    public ValueTask<ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>> ReadOpsCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContractAsync<IndexOpsCatalogJsonContract, OpsCatalogDescriptorSnapshot>(
            unityProject,
            UcliStoragePathResolver.ResolveOpsCatalogPath,
            static json => IndexOpsCatalogJsonContractSerializer.Deserialize(json),
            static contract => OpsCatalogDescriptorSnapshot.TryCreate(contract, out var snapshot)
                ? snapshot
                : null,
            "ops.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>ops.describe/&lt;opKey&gt;.json</c> contract referenced by <c>ops.catalog.json</c>. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="catalogEntry"> The lightweight catalog entry that references the detail artifact. </param>
    /// <param name="sourceInputsHash"> The expected source-inputs hash from <c>ops.catalog.json</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to describe-artifact read result. </returns>
    public async ValueTask<ReadIndexArtifactReadResult<OpsDescribeSnapshot>> ReadOpsDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        ValidatedOpsCatalogEntry catalogEntry,
        Sha256Digest sourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(catalogEntry);
        ArgumentNullException.ThrowIfNull(sourceInputsHash);

        string contractPath;
        try
        {
            contractPath = UcliStoragePathResolver.ResolveOpsDescribePath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                catalogEntry.DescribeKey);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid. {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file 'ops.catalog.json' is malformed. {ex.Message}");
        }

        const string contractName = "catalogs/ops.describe/<opKey>.json";
        if (!File.Exists(contractPath))
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Index contract file was not found: {contractName}.");
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(contractPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid: {contractPath}. {ex.Message}");
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Failed to read index contract file '{contractName}'. {ex.Message}");
        }

        if (json is null)
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Index contract file was not found: {contractName}.");
        }

        var actualHash = Sha256Digest.Compute(Encoding.UTF8.GetBytes(json));
        if (actualHash != catalogEntry.DescribeHash)
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. describeHash does not match.");
        }

        IndexOpsDescribeJsonContract? contract;
        try
        {
            contract = IndexOpsDescribeJsonContractSerializer.Deserialize(json);
        }
        catch (ArgumentException ex)
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }
        catch (JsonException ex)
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }

        if (!OpsDescribeSnapshot.TryCreate(contract, out var snapshot))
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed.");
        }

        if (snapshot.SourceInputsHash != sourceInputsHash)
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. sourceInputsHash does not match ops.catalog.json.");
        }

        var operation = snapshot.Operation;
        if (!string.Equals(operation.Name, catalogEntry.Name, StringComparison.Ordinal)
            || operation.Kind != catalogEntry.Kind
            || operation.Policy != catalogEntry.Policy
            || !string.Equals(operation.Description, catalogEntry.Description, StringComparison.Ordinal))
        {
            return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. operation descriptor does not match ops.catalog.json.");
        }

        return ReadIndexArtifactReadResult<OpsDescribeSnapshot>.Success(snapshot);
    }

    /// <summary> Reads one <c>asset-search.lookup.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<AssetSearchLookupSnapshot>> ReadAssetSearchLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContractAsync<IndexAssetSearchLookupJsonContract, AssetSearchLookupSnapshot>(
            unityProject,
            UcliStoragePathResolver.ResolveAssetSearchLookupPath,
            static json => IndexAssetSearchLookupJsonContractSerializer.Deserialize(json),
            static contract => AssetSearchLookupSnapshot.TryCreate(contract, out var snapshot)
                ? snapshot
                : null,
            "lookups/asset-search.lookup.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>guid-path.lookup.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<GuidPathLookupSnapshot>> ReadGuidPathLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContractAsync<IndexGuidPathLookupJsonContract, GuidPathLookupSnapshot>(
            unityProject,
            UcliStoragePathResolver.ResolveGuidPathLookupPath,
            static json => IndexGuidPathLookupJsonContractSerializer.Deserialize(json),
            static contract => GuidPathLookupSnapshot.TryCreate(contract, out var snapshot)
                ? snapshot
                : null,
            "lookups/guid-path.lookup.json",
            cancellationToken);
    }

    /// <summary> Reads one scene-tree-lite lookup contract for the specified scene path. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="scenePath"> The project-relative scene path represented by the lookup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to lookup-read result. </returns>
    public async ValueTask<ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>> ReadSceneTreeLiteLookupAsync (
        ResolvedUnityProjectContext unityProject,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(scenePath);

        string contractPath;
        try
        {
            contractPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                scenePath.Value);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid. {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                ex.Message);
        }

        var result = await ReadContractAsync<IndexSceneTreeLiteLookupJsonContract, SceneTreeLiteLookupSnapshot>(
                contractPath,
                static json => IndexSceneTreeLiteLookupJsonContractSerializer.Deserialize(json),
                static contract => SceneTreeLiteLookupSnapshot.TryCreate(contract, out var snapshot)
                    ? snapshot
                    : null,
                "lookups/scene-tree-lite/*.lookup.json",
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        if (result.Value!.ScenePath != scenePath)
        {
            return ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'lookups/scene-tree-lite/*.lookup.json' is malformed. scenePath does not match the requested scene path.");
        }

        return result;
    }

    /// <summary> Reads one <c>inputs/manifest.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to manifest-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<ReadIndexInputsManifestSnapshot>> ReadInputsManifestAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadContractAsync<IndexInputsManifestJsonContract, ReadIndexInputsManifestSnapshot>(
            unityProject,
            UcliStoragePathResolver.ResolveIndexInputsManifestPath,
            static json => IndexInputsManifestJsonContractSerializer.Deserialize(json),
            static contract => ReadIndexInputsManifestSnapshot.TryCreate(contract, out var snapshot)
                ? snapshot
                : null,
            "inputs/manifest.json",
            cancellationToken);
    }

    private static async ValueTask<ReadIndexArtifactReadResult<TArtifact>> ReadContractAsync<TContract, TArtifact> (
        ResolvedUnityProjectContext unityProject,
        Func<string, ProjectFingerprint, string> pathResolver,
        Func<string, TContract?> deserialize,
        Func<TContract, TArtifact?> project,
        string contractName,
        CancellationToken cancellationToken)
        where TContract : class
        where TArtifact : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        string contractPath;
        try
        {
            contractPath = pathResolver(unityProject.RepositoryRoot, unityProject.ProjectFingerprint);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid. {ex.Message}");
        }

        return await ReadContractAsync(
                contractPath,
                deserialize,
                project,
                contractName,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<ReadIndexArtifactReadResult<TArtifact>> ReadContractAsync<TContract, TArtifact> (
        string contractPath,
        Func<string, TContract?> deserialize,
        Func<TContract, TArtifact?> project,
        string contractName,
        CancellationToken cancellationToken)
        where TContract : class
        where TArtifact : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(contractPath))
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Index contract file was not found: {contractName}.");
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(contractPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid: {contractPath}. {ex.Message}");
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Failed to read index contract file '{contractName}'. {ex.Message}");
        }

        if (json is null)
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Index contract file was not found: {contractName}.");
        }

        TContract? contract;
        try
        {
            contract = deserialize(json);
        }
        catch (ArgumentException ex)
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }
        catch (JsonException ex)
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed. {ex.Message}");
        }

        var artifact = contract == null ? null : project(contract);
        if (artifact == null)
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Index contract file '{contractName}' is malformed.");
        }

        return ReadIndexArtifactReadResult<TArtifact>.Success(artifact);
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException;
    }
}
