namespace MackySoft.Ucli.Tests.Helpers.OperationCatalog;

internal sealed class StubPersistedOpsCatalogPersistenceArtifactsReader : IPersistedOpsCatalogPersistenceArtifactsReader
{
    public PersistedOpsCatalogPersistenceArtifacts Result { get; set; }
        = new(InputsManifest: null, HasPersistedAssetLookupArtifacts: false);

    public ValueTask<PersistedOpsCatalogPersistenceArtifacts> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result);
    }
}
