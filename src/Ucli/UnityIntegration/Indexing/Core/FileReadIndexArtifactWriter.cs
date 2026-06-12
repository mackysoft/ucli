using System.Text;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Persists read-index artifacts to local storage paths. </summary>
internal sealed class FileReadIndexArtifactWriter : IReadIndexArtifactWriter
{
    private const int SchemaVersion = 1;

    private readonly IJsonContractWriter<IndexOpsCatalogJsonContract> opsCatalogWriter;

    private readonly IJsonContractWriter<IndexOpsDescribeJsonContract> opsDescribeWriter;

    private readonly IJsonContractWriter<IndexAssetSearchLookupJsonContract> assetSearchLookupWriter;

    private readonly IJsonContractWriter<IndexGuidPathLookupJsonContract> guidPathLookupWriter;

    private readonly IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract> sceneTreeLiteLookupWriter;

    private readonly IJsonContractWriter<IndexInputsManifestJsonContract> inputsManifestWriter;

    /// <summary> Initializes a new instance of the <see cref="FileReadIndexArtifactWriter" /> class. </summary>
    public FileReadIndexArtifactWriter (
        IJsonContractWriter<IndexOpsCatalogJsonContract> opsCatalogWriter,
        IJsonContractWriter<IndexOpsDescribeJsonContract> opsDescribeWriter,
        IJsonContractWriter<IndexAssetSearchLookupJsonContract> assetSearchLookupWriter,
        IJsonContractWriter<IndexGuidPathLookupJsonContract> guidPathLookupWriter,
        IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract> sceneTreeLiteLookupWriter,
        IJsonContractWriter<IndexInputsManifestJsonContract> inputsManifestWriter)
    {
        this.opsCatalogWriter = opsCatalogWriter ?? throw new ArgumentNullException(nameof(opsCatalogWriter));
        this.opsDescribeWriter = opsDescribeWriter ?? throw new ArgumentNullException(nameof(opsDescribeWriter));
        this.assetSearchLookupWriter = assetSearchLookupWriter ?? throw new ArgumentNullException(nameof(assetSearchLookupWriter));
        this.guidPathLookupWriter = guidPathLookupWriter ?? throw new ArgumentNullException(nameof(guidPathLookupWriter));
        this.sceneTreeLiteLookupWriter = sceneTreeLiteLookupWriter ?? throw new ArgumentNullException(nameof(sceneTreeLiteLookupWriter));
        this.inputsManifestWriter = inputsManifestWriter ?? throw new ArgumentNullException(nameof(inputsManifestWriter));
    }

    /// <inheritdoc />
    public async ValueTask WriteOpsCatalogAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        string sourceInputsHash,
        ReadIndexInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInputsHash);
        cancellationToken.ThrowIfCancellationRequested();

        var opsCatalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(storageRoot, projectFingerprint);
        var opsDescribeDirectoryPath = UcliStoragePathResolver.ResolveOpsDescribeDirectory(storageRoot, projectFingerprint);
        EnsureParentDirectory(opsCatalogPath);
        FileSystemAccessBoundary.EnsureSecureDirectory(opsDescribeDirectoryPath);

        var orderedOperations = IndexJsonOrderingPolicy.OrderOpsEntries(operations);
        var catalogEntries = new List<IndexOpsCatalogEntryJsonContract>(orderedOperations.Count);
        var originalDescribeArtifacts = new Dictionary<string, string?>(StringComparer.Ordinal);
        var catalogWritten = false;
        try
        {
            for (var i = 0; i < orderedOperations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var operation = orderedOperations[i];
                if (string.IsNullOrWhiteSpace(operation.Name))
                {
                    throw new InvalidOperationException("Operation name must not be empty when writing ops read-index artifacts.");
                }

                var describeKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(operation.Name));
                var describeContract = new IndexOpsDescribeJsonContract(
                    SchemaVersion: SchemaVersion,
                    GeneratedAtUtc: generatedAtUtc,
                    SourceInputsHash: sourceInputsHash,
                    Operation: operation);
                var describeJson = opsDescribeWriter.Write(describeContract);
                var describeHash = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(describeJson));
                var describePath = UcliStoragePathResolver.ResolveOpsDescribePath(
                    storageRoot,
                    projectFingerprint,
                    describeKey);

                await CaptureOriginalDescribeArtifactAsync(describePath, originalDescribeArtifacts, cancellationToken).ConfigureAwait(false);
                await FileUtilities.WriteAllTextAtomicallyAsync(
                        describePath,
                        describeJson,
                        cancellationToken)
                    .ConfigureAwait(false);

                catalogEntries.Add(
                    new IndexOpsCatalogEntryJsonContract(
                        Name: operation.Name,
                        Kind: operation.Kind,
                        Policy: operation.Policy,
                        Description: operation.Description,
                        DescribeKey: describeKey,
                        DescribeHash: describeHash));
            }

            var opsCatalog = new IndexOpsCatalogJsonContract(
                SchemaVersion: SchemaVersion,
                GeneratedAtUtc: generatedAtUtc,
                SourceInputsHash: sourceInputsHash,
                Entries: catalogEntries);

            await FileUtilities.WriteAllTextAtomicallyAsync(
                    opsCatalogPath,
                    opsCatalogWriter.Write(opsCatalog),
                    cancellationToken)
                .ConfigureAwait(false);
            catalogWritten = true;
        }
        catch
        {
            if (!catalogWritten)
            {
                await RestoreDescribeArtifactsAsync(originalDescribeArtifacts).ConfigureAwait(false);
            }

            throw;
        }

        if (manifestInputSnapshot != null)
        {
            await WriteInputsManifestAsync(
                    storageRoot,
                    projectFingerprint,
                    generatedAtUtc,
                    manifestInputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAssetLookupsAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentNullException.ThrowIfNull(assetSearchEntries);
        ArgumentNullException.ThrowIfNull(guidPathEntries);
        ArgumentNullException.ThrowIfNull(inputSnapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var assetSearchLookupPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(storageRoot, projectFingerprint);
        var guidPathLookupPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(storageRoot, projectFingerprint);
        EnsureParentDirectory(assetSearchLookupPath);
        EnsureParentDirectory(guidPathLookupPath);

        var assetSearchLookup = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.AssetSearchHash,
            Entries: assetSearchEntries);
        var guidPathLookup = new IndexGuidPathLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.GuidPathHash,
            Entries: guidPathEntries);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                assetSearchLookupPath,
                assetSearchLookupWriter.Write(assetSearchLookup),
                cancellationToken)
            .ConfigureAwait(false);
        await FileUtilities.WriteAllTextAtomicallyAsync(
                guidPathLookupPath,
                guidPathLookupWriter.Write(guidPathLookup),
                cancellationToken)
            .ConfigureAwait(false);
        await WriteInputsManifestAsync(storageRoot, projectFingerprint, generatedAtUtc, inputSnapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask WriteSceneTreeLiteAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        string scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        string sourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInputsHash);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedScenePath = PathStringNormalizer.ToSlashSeparated(scenePath);
        var lookupPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(storageRoot, projectFingerprint, normalizedScenePath);
        EnsureParentDirectory(lookupPath);

        var lookup = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScenePath: normalizedScenePath,
            SourceInputsHash: sourceInputsHash,
            Roots: roots);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                lookupPath,
                sceneTreeLiteLookupWriter.Write(lookup),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask CaptureOriginalDescribeArtifactAsync (
        string describePath,
        Dictionary<string, string?> originalDescribeArtifacts,
        CancellationToken cancellationToken)
    {
        if (originalDescribeArtifacts.ContainsKey(describePath))
        {
            return;
        }

        var originalJson = await FileUtilities.ReadAllTextOrNullAsync(describePath, cancellationToken).ConfigureAwait(false);
        originalDescribeArtifacts.Add(describePath, originalJson);
    }

    private static async ValueTask RestoreDescribeArtifactsAsync (Dictionary<string, string?> originalDescribeArtifacts)
    {
        // NOTE: ops.catalog.json is the commit point for split ops artifacts. Before that point,
        // existing detail files must remain compatible with the old catalog after cancellation or failure.
        foreach (var artifact in originalDescribeArtifacts)
        {
            if (artifact.Value == null)
            {
                FileUtilities.DeleteIfExists(artifact.Key);
                continue;
            }

            await FileUtilities.WriteAllTextAtomicallyAsync(
                    artifact.Key,
                    artifact.Value,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask WriteInputsManifestAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken)
    {
        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);
        EnsureParentDirectory(inputsManifestPath);

        var inputsManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScriptAssembliesHash: inputSnapshot.ScriptAssembliesHash,
            PackagesManifestHash: inputSnapshot.PackagesManifestHash,
            PackagesLockHash: inputSnapshot.PackagesLockHash,
            AssemblyDefinitionHash: inputSnapshot.AssemblyDefinitionHash,
            AssetsContentHash: inputSnapshot.AssetsContentHash,
            AssetSearchHash: inputSnapshot.AssetSearchHash,
            GuidPathHash: inputSnapshot.GuidPathHash,
            CombinedHash: inputSnapshot.CombinedHash);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                inputsManifestPath,
                inputsManifestWriter.Write(inputsManifest),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void EnsureParentDirectory (string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {filePath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
    }
}
