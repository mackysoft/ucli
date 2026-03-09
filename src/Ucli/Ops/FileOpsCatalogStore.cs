using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Ops;

/// <summary> Persists <c>ops.catalog.json</c> and <c>inputs/manifest.json</c> to the local read-index store. </summary>
internal sealed class FileOpsCatalogStore : IOpsCatalogStore
{
    private const int SchemaVersion = 1;

    /// <inheritdoc />
    public async ValueTask Write (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        IndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(inputSnapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var opsCatalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(storageRoot, projectFingerprint);
        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);

        var opsCatalog = new IndexOpsCatalogJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.CombinedHash,
            Entries: operations.ToArray());
        var inputsManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScriptAssembliesHash: inputSnapshot.ScriptAssembliesHash,
            PackagesManifestHash: inputSnapshot.PackagesManifestHash,
            PackagesLockHash: inputSnapshot.PackagesLockHash,
            AssemblyDefinitionHash: inputSnapshot.AssemblyDefinitionHash,
            CombinedHash: inputSnapshot.CombinedHash);

        await FileUtilities.WriteAllTextAtomically(
                opsCatalogPath,
                IndexOpsCatalogJsonContractSerializer.Serialize(opsCatalog) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
        await FileUtilities.WriteAllTextAtomically(
                inputsManifestPath,
                IndexInputsManifestJsonContractSerializer.Serialize(inputsManifest) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
