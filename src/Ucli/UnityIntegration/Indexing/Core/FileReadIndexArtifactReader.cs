using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Provides filesystem-backed access to read-index artifact contract files. </summary>
internal sealed class FileReadIndexArtifactReader : IReadIndexArtifactReader
{
    private readonly FileReadIndexGenerationStore generationStore;

    /// <summary> Initializes a new instance of the <see cref="FileReadIndexArtifactReader" /> class. </summary>
    /// <param name="generationStore"> The immutable generation resolver. </param>
    public FileReadIndexArtifactReader (FileReadIndexGenerationStore generationStore)
    {
        this.generationStore = generationStore ?? throw new ArgumentNullException(nameof(generationStore));
    }

    /// <inheritdoc />
    public async ValueTask<ReadIndexGenerationArtifacts> ReadGenerationArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var resolution = await ResolveCurrentGenerationAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (resolution.Error != null)
        {
            return CreateFailedGeneration(resolution.Error);
        }

        var generationDirectoryPath = resolution.DirectoryPath!;
        var opsCatalog = await ReadContractAsync<IndexOpsCatalogJsonContract, OpsCatalogDescriptorSnapshot>(
                Path.Combine(generationDirectoryPath, UcliStoragePathNames.OpsCatalogFileName),
                static json => IndexOpsCatalogJsonContractSerializer.Deserialize(json),
                static contract => OpsCatalogDescriptorSnapshot.TryCreate(contract, out var snapshot) ? snapshot : null,
                "ops.catalog.json",
                cancellationToken)
            .ConfigureAwait(false);
        var assetSearchLookup = await ReadContractAsync<IndexAssetSearchLookupJsonContract, AssetSearchLookupSnapshot>(
                Path.Combine(generationDirectoryPath, UcliStoragePathNames.AssetSearchLookupFileName),
                static json => IndexAssetSearchLookupJsonContractSerializer.Deserialize(json),
                static contract => AssetSearchLookupSnapshot.TryCreate(contract, out var snapshot) ? snapshot : null,
                "lookups/asset-search.lookup.json",
                cancellationToken)
            .ConfigureAwait(false);
        var guidPathLookup = await ReadContractAsync<IndexGuidPathLookupJsonContract, GuidPathLookupSnapshot>(
                Path.Combine(generationDirectoryPath, UcliStoragePathNames.GuidPathLookupFileName),
                static json => IndexGuidPathLookupJsonContractSerializer.Deserialize(json),
                static contract => GuidPathLookupSnapshot.TryCreate(contract, out var snapshot) ? snapshot : null,
                "lookups/guid-path.lookup.json",
                cancellationToken)
            .ConfigureAwait(false);
        var inputsManifest = await ReadContractAsync<IndexInputsManifestJsonContract, ReadIndexInputsManifestSnapshot>(
                Path.Combine(generationDirectoryPath, UcliStoragePathNames.IndexInputsManifestFileName),
                static json => IndexInputsManifestJsonContractSerializer.Deserialize(json),
                static contract => ReadIndexInputsManifestSnapshot.TryCreate(contract, out var snapshot) ? snapshot : null,
                "inputs/manifest.json",
                cancellationToken)
            .ConfigureAwait(false);

        return new ReadIndexGenerationArtifacts(
            opsCatalog,
            assetSearchLookup,
            guidPathLookup,
            inputsManifest);
    }

    /// <summary> Reads one <c>ops.catalog.json</c> contract. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to catalog-read result. </returns>
    public ValueTask<ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>> ReadOpsCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadGenerationContractAsync<IndexOpsCatalogJsonContract, OpsCatalogDescriptorSnapshot>(
            unityProject,
            UcliStoragePathNames.OpsCatalogFileName,
            static json => IndexOpsCatalogJsonContractSerializer.Deserialize(json),
            static contract => OpsCatalogDescriptorSnapshot.TryCreate(contract, out var snapshot)
                ? snapshot
                : null,
            "ops.catalog.json",
            cancellationToken);
    }

    /// <summary> Reads one <c>ops/&lt;operationStorageKey&gt;.json</c> contract referenced by <c>ops.catalog.json</c>. </summary>
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

        const string contractName = "ops/<operationStorageKey>.json";
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
        return ReadGenerationContractAsync<IndexAssetSearchLookupJsonContract, AssetSearchLookupSnapshot>(
            unityProject,
            UcliStoragePathNames.AssetSearchLookupFileName,
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
        return ReadGenerationContractAsync<IndexGuidPathLookupJsonContract, GuidPathLookupSnapshot>(
            unityProject,
            UcliStoragePathNames.GuidPathLookupFileName,
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
                "scenes/<sceneStorageKey>.json",
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
                "Index contract file 'scenes/<sceneStorageKey>.json' is malformed. scenePath does not match the requested scene path.");
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
        return ReadGenerationContractAsync<IndexInputsManifestJsonContract, ReadIndexInputsManifestSnapshot>(
            unityProject,
            UcliStoragePathNames.IndexInputsManifestFileName,
            static json => IndexInputsManifestJsonContractSerializer.Deserialize(json),
            static contract => ReadIndexInputsManifestSnapshot.TryCreate(contract, out var snapshot)
                ? snapshot
                : null,
            "inputs/manifest.json",
            cancellationToken);
    }

    private async ValueTask<ReadIndexArtifactReadResult<TArtifact>> ReadGenerationContractAsync<TContract, TArtifact> (
        ResolvedUnityProjectContext unityProject,
        string fileName,
        Func<string, TContract?> deserialize,
        Func<TContract, TArtifact?> project,
        string contractName,
        CancellationToken cancellationToken)
        where TContract : class
        where TArtifact : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var resolution = await ResolveCurrentGenerationAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (resolution.Error != null)
        {
            return ReadIndexArtifactReadResult<TArtifact>.Failure(resolution.Error);
        }

        return await ReadContractAsync(
                Path.Combine(resolution.DirectoryPath!, fileName),
                deserialize,
                project,
                contractName,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(string? DirectoryPath, IndexServiceError? Error)> ResolveCurrentGenerationAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        string? generationDirectoryPath;
        try
        {
            generationDirectoryPath = await generationStore.ResolveCurrentDirectoryAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return (null, new IndexServiceError(
                UcliCoreErrorCodes.InvalidArgument,
                $"Index path is invalid. {ex.Message}"));
        }
        catch (InvalidDataException ex)
        {
            return (null, new IndexServiceError(
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                $"Current index generation is malformed. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return (null, new IndexServiceError(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                $"Failed to resolve the current index generation. {ex.Message}"));
        }

        if (generationDirectoryPath == null)
        {
            return (null, new IndexServiceError(
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                "No read-index generation has committed."));
        }

        return (generationDirectoryPath, null);
    }

    private static ReadIndexGenerationArtifacts CreateFailedGeneration (IndexServiceError error)
    {
        return new ReadIndexGenerationArtifacts(
            ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>.Failure(error),
            ReadIndexArtifactReadResult<AssetSearchLookupSnapshot>.Failure(error),
            ReadIndexArtifactReadResult<GuidPathLookupSnapshot>.Failure(error),
            ReadIndexArtifactReadResult<ReadIndexInputsManifestSnapshot>.Failure(error));
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
