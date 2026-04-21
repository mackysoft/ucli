using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Tests;

public sealed class ReadIndexValidationCatalogResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenReadIndexDisabled_ReturnsSyntaxOnlySuccess ()
    {
        var loader = new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Success(
                CreateSnapshot(IndexFreshness.Fresh).Entries,
                CreateSnapshot(IndexFreshness.Fresh).GeneratedAtUtc,
                CreateSnapshot(IndexFreshness.Fresh).Freshness));
        var resolver = new ReadIndexValidationCatalogResolver(loader);

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.Disabled,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Catalog.IsAvailable);
        Assert.False(result.ReadIndex.Used);
        Assert.False(result.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoTextCodec.SourceIndex, result.ReadIndex.Source);
        Assert.Equal(ReadIndexInfoTextCodec.FreshnessProbable, result.ReadIndex.Freshness);
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
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Success(
                CreateSnapshot(IndexFreshness.Stale).Entries,
                CreateSnapshot(IndexFreshness.Stale).GeneratedAtUtc,
                CreateSnapshot(IndexFreshness.Stale).Freshness)));

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.RequireFresh,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.ReadIndexFreshRequired, result.ErrorCode);
        Assert.True(result.ReadIndex.Used);
        Assert.True(result.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoTextCodec.FreshnessStale, result.ReadIndex.Freshness);
        Assert.Contains("requireFresh", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSnapshotEntryIsMalformed_ReturnsFormatInvalidFailure ()
    {
        var malformedSnapshot = new PersistedOpsCatalogSnapshot(
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: "ucli.invalid",
                    Kind: "unsupported-kind",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}"""),
            ],
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            Freshness: IndexFreshness.Fresh);
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
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogSnapshotLoader(
            PersistedOpsCatalogReadResult.Success(
                CreateSnapshot(IndexFreshness.Probable).Entries,
                CreateSnapshot(IndexFreshness.Probable).GeneratedAtUtc,
                CreateSnapshot(IndexFreshness.Probable).Freshness)));

        var result = await resolver.Resolve(
            CreateUnityProject(),
            ReadIndexMode.AllowStale,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Catalog.IsAvailable);
        Assert.Single(result.Catalog.Operations);
        Assert.True(result.ReadIndex.Used);
        Assert.True(result.ReadIndex.Hit);
        Assert.Equal(ReadIndexInfoTextCodec.FreshnessProbable, result.ReadIndex.Freshness);
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

    private static PersistedOpsCatalogSnapshot CreateSnapshot (IndexFreshness freshness)
    {
        return new PersistedOpsCatalogSnapshot(
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
