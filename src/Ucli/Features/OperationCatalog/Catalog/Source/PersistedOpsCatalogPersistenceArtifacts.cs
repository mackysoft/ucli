using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents persisted read-index artifacts that can be reused when persisting one refreshed ops catalog. </summary>
/// <param name="InputsManifest"> The persisted inputs manifest when available; otherwise <see langword="null" />. </param>
/// <param name="HasPersistedAssetLookupArtifacts"> A value indicating whether persisted asset lookup artifacts still exist. </param>
internal sealed record PersistedOpsCatalogPersistenceArtifacts (
    ReadIndexInputsManifestSnapshot? InputsManifest,
    bool HasPersistedAssetLookupArtifacts);
