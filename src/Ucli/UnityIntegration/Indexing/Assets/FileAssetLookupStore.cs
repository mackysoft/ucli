using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Persists asset lookup artifacts and input manifest to the local read-index store. </summary>
internal sealed class FileAssetLookupStore : IAssetLookupStore
{
    private const int SchemaVersion = 1;

    private readonly IJsonContractWriter<IndexAssetSearchLookupJsonContract> assetSearchLookupWriter;

    private readonly IJsonContractWriter<IndexGuidPathLookupJsonContract> guidPathLookupWriter;

    private readonly IJsonContractWriter<IndexInputsManifestJsonContract> inputsManifestWriter;

    /// <summary> Initializes a new instance of the <see cref="FileAssetLookupStore" /> class. </summary>
    /// <param name="assetSearchLookupWriter"> The writer for <c>asset-search.lookup.json</c>. </param>
    /// <param name="guidPathLookupWriter"> The writer for <c>guid-path.lookup.json</c>. </param>
    /// <param name="inputsManifestWriter"> The writer for <c>inputs/manifest.json</c>. </param>
    public FileAssetLookupStore (
        IJsonContractWriter<IndexAssetSearchLookupJsonContract> assetSearchLookupWriter,
        IJsonContractWriter<IndexGuidPathLookupJsonContract> guidPathLookupWriter,
        IJsonContractWriter<IndexInputsManifestJsonContract> inputsManifestWriter)
    {
        this.assetSearchLookupWriter = assetSearchLookupWriter ?? throw new ArgumentNullException(nameof(assetSearchLookupWriter));
        this.guidPathLookupWriter = guidPathLookupWriter ?? throw new ArgumentNullException(nameof(guidPathLookupWriter));
        this.inputsManifestWriter = inputsManifestWriter ?? throw new ArgumentNullException(nameof(inputsManifestWriter));
    }

    /// <inheritdoc />
    public async ValueTask Write (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        IndexInputHashSnapshot inputSnapshot,
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
        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);
        EnsureParentDirectory(assetSearchLookupPath);
        EnsureParentDirectory(guidPathLookupPath);
        EnsureParentDirectory(inputsManifestPath);

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

        await FileUtilities.WriteAllTextAtomically(
                assetSearchLookupPath,
                assetSearchLookupWriter.Write(assetSearchLookup),
                cancellationToken)
            .ConfigureAwait(false);
        await FileUtilities.WriteAllTextAtomically(
                guidPathLookupPath,
                guidPathLookupWriter.Write(guidPathLookup),
                cancellationToken)
            .ConfigureAwait(false);
        await FileUtilities.WriteAllTextAtomically(
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
