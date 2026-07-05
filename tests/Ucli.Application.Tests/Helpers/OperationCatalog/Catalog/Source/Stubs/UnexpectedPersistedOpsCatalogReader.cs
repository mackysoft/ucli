using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedPersistedOpsCatalogReader : IPersistedOpsCatalogReader
{
    public ValueTask<PersistedOpsCatalogReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Persisted ops catalog should not be read.");
    }

    public ValueTask<PersistedOpsCatalogDescriptorReadResult> ReadDescriptorsAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Persisted ops catalog descriptors should not be read.");
    }

    public ValueTask<PersistedOpsDescribeReadResult> ReadDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        OpsCatalogDescriptorSnapshot catalogSnapshot,
        IndexOpsCatalogEntryJsonContract catalogEntry,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Persisted ops describe artifact should not be read.");
    }
}
