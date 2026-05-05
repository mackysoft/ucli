using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

/// <summary> Adapts persisted read-index support artifacts to the operation-catalog persistence seam. </summary>
internal sealed class PersistedOpsCatalogPersistenceArtifactsReader : IPersistedOpsCatalogPersistenceArtifactsReader
{
    private readonly IIndexCatalogReader indexCatalogReader;

    /// <summary> Initializes a new instance of the <see cref="PersistedOpsCatalogPersistenceArtifactsReader" /> class. </summary>
    /// <param name="indexCatalogReader"> The persisted index reader dependency. </param>
    public PersistedOpsCatalogPersistenceArtifactsReader (IIndexCatalogReader indexCatalogReader)
    {
        this.indexCatalogReader = indexCatalogReader ?? throw new ArgumentNullException(nameof(indexCatalogReader));
    }

    /// <inheritdoc />
    public async ValueTask<PersistedOpsCatalogPersistenceArtifacts> Read (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var manifestResult = await indexCatalogReader.ReadInputsManifest(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (manifestResult.IsSuccess)
        {
            return new PersistedOpsCatalogPersistenceArtifacts(
                InputsManifest: manifestResult.Value!,
                HasPersistedAssetLookupArtifacts: true);
        }

        var assetSearchLookupResult = await indexCatalogReader.ReadAssetSearchLookup(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (assetSearchLookupResult.IsSuccess)
        {
            return new PersistedOpsCatalogPersistenceArtifacts(
                InputsManifest: null,
                HasPersistedAssetLookupArtifacts: true);
        }

        var guidPathLookupResult = await indexCatalogReader.ReadGuidPathLookup(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        return new PersistedOpsCatalogPersistenceArtifacts(
            InputsManifest: null,
            HasPersistedAssetLookupArtifacts: guidPathLookupResult.IsSuccess);
    }
}
