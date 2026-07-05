using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingPersistedOpsCatalogReader : IPersistedOpsCatalogReader
{
    private readonly List<ReadInvocation> readInvocations = [];

    private readonly List<ReadInvocation> readDescriptorInvocations = [];

    private readonly List<ReadDescribeInvocation> readDescribeInvocations = [];

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public IReadOnlyList<ReadInvocation> ReadDescriptorInvocations => readDescriptorInvocations;

    public IReadOnlyList<ReadDescribeInvocation> ReadDescribeInvocations => readDescribeInvocations;

    public PersistedOpsCatalogReadResult? ReadResult { get; set; }

    public PersistedOpsCatalogDescriptorReadResult? DescriptorResult { get; set; }

    public PersistedOpsDescribeReadResult? DescribeResult { get; set; }

    public ValueTask<PersistedOpsCatalogReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        readInvocations.Add(new ReadInvocation(unityProject, cancellationToken));
        return ValueTask.FromResult(GetReadResult());
    }

    public ValueTask<PersistedOpsCatalogDescriptorReadResult> ReadDescriptorsAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        readDescriptorInvocations.Add(new ReadInvocation(unityProject, cancellationToken));

        if (DescriptorResult != null)
        {
            return ValueTask.FromResult(DescriptorResult);
        }

        var readResult = GetReadResult();
        if (!readResult.IsSuccess)
        {
            return ValueTask.FromResult(PersistedOpsCatalogDescriptorReadResult.Failure(readResult.ReadFailure!));
        }

        return ValueTask.FromResult(PersistedOpsCatalogDescriptorReadResult.Success(
            CreateDescriptorSnapshot(readResult.Snapshot!),
            readResult.Freshness!.Value));
    }

    public ValueTask<PersistedOpsDescribeReadResult> ReadDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        OpsCatalogDescriptorSnapshot catalogSnapshot,
        IndexOpsCatalogEntryJsonContract catalogEntry,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(catalogSnapshot);
        ArgumentNullException.ThrowIfNull(catalogEntry);
        readDescribeInvocations.Add(new ReadDescribeInvocation(
            unityProject,
            catalogSnapshot,
            catalogEntry,
            cancellationToken));

        if (DescribeResult != null)
        {
            return ValueTask.FromResult(DescribeResult);
        }

        var operation = GetReadResult().Snapshot!.Operations.First(
            operation => string.Equals(operation.Name, catalogEntry.Name, StringComparison.Ordinal));
        return ValueTask.FromResult(PersistedOpsDescribeReadResult.Success(operation));
    }

    private PersistedOpsCatalogReadResult GetReadResult ()
    {
        if (ReadResult is null)
        {
            throw new InvalidOperationException("Persisted ops catalog read result is not configured.");
        }

        return ReadResult;
    }

    private static OpsCatalogDescriptorSnapshot CreateDescriptorSnapshot (OpsCatalogSnapshot snapshot)
    {
        var entries = snapshot.Operations
            .Select(static (operation, index) => new IndexOpsCatalogEntryJsonContract(
                operation.Name,
                operation.Kind,
                operation.Policy,
                operation.Description,
                CreateHash(index),
                CreateHash(index + 8)))
            .ToArray();

        if (!OpsCatalogDescriptorSnapshot.TryCreate(
            snapshot.GeneratedAtUtc,
            "source-hash",
            entries,
            "entries",
            out var descriptorSnapshot,
            out var error))
        {
            throw new InvalidOperationException($"Persisted ops catalog descriptor fixture is invalid. {error}");
        }

        return descriptorSnapshot!;
    }

    private static string CreateHash (int index)
    {
        const string HexDigits = "0123456789abcdef";
        return new string(HexDigits[index % HexDigits.Length], 64);
    }

    internal readonly record struct ReadInvocation (
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);

    internal readonly record struct ReadDescribeInvocation (
        ResolvedUnityProjectContext UnityProject,
        OpsCatalogDescriptorSnapshot CatalogSnapshot,
        IndexOpsCatalogEntryJsonContract CatalogEntry,
        CancellationToken CancellationToken);
}
