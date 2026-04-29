using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Storage;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Persistence;

/// <summary> Persists <c>ops.catalog.json</c> and optional <c>inputs/manifest.json</c> to the local read-index store. </summary>
internal sealed class FileOpsCatalogStore : IOpsCatalogStore
{
    private const int SchemaVersion = 1;

    /// <inheritdoc />
    public async ValueTask Write (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        string sourceInputsHash,
        IndexInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInputsHash);
        cancellationToken.ThrowIfCancellationRequested();

        var opsCatalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(storageRoot, projectFingerprint);
        var opsCatalogDirectoryPath = Path.GetDirectoryName(opsCatalogPath)
            ?? throw new InvalidOperationException($"ops.catalog.json directory path could not be resolved: {opsCatalogPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(opsCatalogDirectoryPath);

        var opsCatalog = new IndexOpsCatalogJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: sourceInputsHash,
            Entries: operations.ToArray());

        await FileUtilities.WriteAllTextAtomically(
                opsCatalogPath,
                IndexOpsCatalogJsonContractSerializer.Serialize(opsCatalog) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);

        if (manifestInputSnapshot == null)
        {
            return;
        }

        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);
        var inputsManifestDirectoryPath = Path.GetDirectoryName(inputsManifestPath)
            ?? throw new InvalidOperationException($"inputs manifest directory path could not be resolved: {inputsManifestPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(inputsManifestDirectoryPath);

        var inputsManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScriptAssembliesHash: manifestInputSnapshot.ScriptAssembliesHash,
            PackagesManifestHash: manifestInputSnapshot.PackagesManifestHash,
            PackagesLockHash: manifestInputSnapshot.PackagesLockHash,
            AssemblyDefinitionHash: manifestInputSnapshot.AssemblyDefinitionHash,
            AssetsContentHash: manifestInputSnapshot.AssetsContentHash,
            AssetSearchHash: manifestInputSnapshot.AssetSearchHash,
            GuidPathHash: manifestInputSnapshot.GuidPathHash,
            CombinedHash: manifestInputSnapshot.CombinedHash);
        await FileUtilities.WriteAllTextAtomically(
                inputsManifestPath,
                IndexInputsManifestJsonContractSerializer.Serialize(inputsManifest) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
