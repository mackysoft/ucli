using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Shared.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Persists asset lookup artifacts and input manifest to the local read-index store. </summary>
internal sealed class FileAssetLookupStore : IAssetLookupStore
{
    private const int SchemaVersion = 1;

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

        var orderedAssetSearchEntries = assetSearchEntries
            .OrderBy(static entry => entry.AssetPath ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        var orderedGuidPathEntries = guidPathEntries
            .OrderBy(static entry => entry.AssetPath ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

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
            Entries: orderedAssetSearchEntries);
        var guidPathLookup = new IndexGuidPathLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.GuidPathHash,
            Entries: orderedGuidPathEntries);
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
                IndexAssetSearchLookupJsonContractSerializer.Serialize(assetSearchLookup) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
        await FileUtilities.WriteAllTextAtomically(
                guidPathLookupPath,
                IndexGuidPathLookupJsonContractSerializer.Serialize(guidPathLookup) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
        await FileUtilities.WriteAllTextAtomically(
                inputsManifestPath,
                IndexInputsManifestJsonContractSerializer.Serialize(inputsManifest) + Environment.NewLine,
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