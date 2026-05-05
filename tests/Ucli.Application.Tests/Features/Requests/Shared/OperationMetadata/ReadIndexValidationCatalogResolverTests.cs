using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ReadIndexValidationCatalogResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenReadIndexDisabled_ReturnsSyntaxOnlySuccess ()
    {
        var snapshot = CreateSnapshot(IndexFreshness.Fresh);
        var loader = new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Success(
                snapshot.Entries,
                snapshot.GeneratedAtUtc,
                snapshot.Freshness));
        var resolver = new ReadIndexValidationCatalogResolver(loader);

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.Disabled,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Catalog.IsAvailable);
        Assert.False(result.ReadIndex.Used);
        Assert.False(result.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoSource.Index, result.ReadIndex.Source);
        Assert.Equal(IndexFreshness.Probable, result.ReadIndex.Freshness);
        Assert.Equal("readIndex disabled by mode.", result.ReadIndex.FallbackReason);
        Assert.Equal(0, loader.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenAllowStaleAndSnapshotIsMissing_ReturnsSyntaxOnlySuccess ()
    {
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: ops.catalog.json.")));

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.AllowStale,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Catalog.IsAvailable);
        Assert.False(result.ReadIndex.Used);
        Assert.False(result.ReadIndex.Hit);
        Assert.Contains("ops.catalog.json", result.ReadIndex.FallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenRequireFreshAndSnapshotIsStale_ReturnsFailureWithReadIndexHit ()
    {
        var snapshot = CreateSnapshot(IndexFreshness.Stale);
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Success(
                snapshot.Entries,
                snapshot.GeneratedAtUtc,
                snapshot.Freshness)));

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.RequireFresh,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.ReadIndexFreshRequired, result.ErrorCode);
        Assert.True(result.ReadIndex.Used);
        Assert.True(result.ReadIndex.Hit);
        Assert.Equal(IndexFreshness.Stale, result.ReadIndex.Freshness);
        Assert.Contains("requireFresh", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSnapshotEntryIsMalformed_ReturnsFormatInvalidFailure ()
    {
        var malformedSnapshot = new
        {
            Entries = new IndexOpEntryJsonContract[]
            {
                new(
                    Name: "ucli.invalid",
                    Kind: "unsupported-kind",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}"""),
            },
            GeneratedAtUtc = DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            Freshness = IndexFreshness.Fresh,
        };
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Success(
                malformedSnapshot.Entries,
                malformedSnapshot.GeneratedAtUtc,
                malformedSnapshot.Freshness)));

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.AllowStale,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.ErrorCode);
        Assert.False(result.ReadIndex.Used);
        Assert.False(result.ReadIndex.Hit);
        Assert.Contains("malformed", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSnapshotIsUsable_ReturnsMetadataBackedSuccess ()
    {
        var snapshot = CreateSnapshot(IndexFreshness.Probable);
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Success(
                snapshot.Entries,
                snapshot.GeneratedAtUtc,
                snapshot.Freshness)));

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.AllowStale,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Catalog.IsAvailable);
        Assert.Single(result.Catalog.Operations);
        Assert.True(result.ReadIndex.Used);
        Assert.True(result.ReadIndex.Hit);
        Assert.Equal(IndexFreshness.Probable, result.ReadIndex.Freshness);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"), result.ReadIndex.GeneratedAtUtc);
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static PersistedOpsCatalogSnapshotStub CreateSnapshot (IndexFreshness freshness)
    {
        return new PersistedOpsCatalogSnapshotStub(
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}"""),
            ],
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            Freshness: freshness);
    }

    private sealed record PersistedOpsCatalogSnapshotStub (
        IReadOnlyList<IndexOpEntryJsonContract> Entries,
        DateTimeOffset GeneratedAtUtc,
        IndexFreshness Freshness);

    private sealed class SpyPersistedOpsCatalogSnapshotLoader : IPersistedOpsCatalogReader
    {
        private readonly PersistedOpsCatalogReadResult result;

        public SpyPersistedOpsCatalogSnapshotLoader (PersistedOpsCatalogReadResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public ValueTask<PersistedOpsCatalogReadResult> Read (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
