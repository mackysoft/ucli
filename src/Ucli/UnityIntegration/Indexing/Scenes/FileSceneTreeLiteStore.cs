using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Persists scene-tree-lite lookup artifacts to the local read-index store. </summary>
internal sealed class FileSceneTreeLiteStore : ISceneTreeLiteStore
{
    private const int SchemaVersion = 1;

    private readonly IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract> sceneTreeLiteLookupWriter;

    /// <summary> Initializes a new instance of the <see cref="FileSceneTreeLiteStore" /> class. </summary>
    /// <param name="sceneTreeLiteLookupWriter"> The writer for scene-tree-lite lookup JSON. </param>
    public FileSceneTreeLiteStore (IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract> sceneTreeLiteLookupWriter)
    {
        this.sceneTreeLiteLookupWriter = sceneTreeLiteLookupWriter ?? throw new ArgumentNullException(nameof(sceneTreeLiteLookupWriter));
    }

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
                sceneTreeLiteLookupWriter.Write(lookup),
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
