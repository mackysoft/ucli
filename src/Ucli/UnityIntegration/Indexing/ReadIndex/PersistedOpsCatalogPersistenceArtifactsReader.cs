using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Adapts persisted read-index support artifacts to the operation-catalog persistence seam. </summary>
internal sealed class PersistedOpsCatalogPersistenceArtifactsReader : IPersistedOpsCatalogPersistenceArtifactsReader
{
    private readonly IReadIndexArtifactReader artifactReader;

    /// <summary> Initializes a new instance of the <see cref="PersistedOpsCatalogPersistenceArtifactsReader" /> class. </summary>
    /// <param name="artifactReader"> The persisted read-index artifact reader dependency. </param>
    public PersistedOpsCatalogPersistenceArtifactsReader (IReadIndexArtifactReader artifactReader)
    {
        this.artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsCatalogPersistenceArtifacts> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var generation = await artifactReader.ReadGenerationArtifactsAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        var manifestResult = generation.InputsManifest;
        if (manifestResult.IsSuccess)
        {
            return new PersistedOpsCatalogPersistenceArtifacts(
                InputsManifest: manifestResult.Value!,
                HasPersistedAssetLookupArtifacts: true);
        }

        var assetSearchLookupResult = generation.AssetSearchLookup;
        if (assetSearchLookupResult.IsSuccess)
        {
            return new PersistedOpsCatalogPersistenceArtifacts(
                InputsManifest: null,
                HasPersistedAssetLookupArtifacts: true);
        }

        var guidPathLookupResult = generation.GuidPathLookup;
        return new PersistedOpsCatalogPersistenceArtifacts(
            InputsManifest: null,
            HasPersistedAssetLookupArtifacts: guidPathLookupResult.IsSuccess);
    }
}
