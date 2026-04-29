using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Shared.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Persists scene-tree-lite lookup artifacts to the local read-index store. </summary>
internal sealed class FileSceneTreeLiteStore : ISceneTreeLiteStore
{
    private const int SchemaVersion = 1;

    /// <inheritdoc />
    public async ValueTask Write (
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

        await FileUtilities.WriteAllTextAtomically(
                lookupPath,
                IndexSceneTreeLiteLookupJsonContractSerializer.Serialize(lookup) + Environment.NewLine,
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
