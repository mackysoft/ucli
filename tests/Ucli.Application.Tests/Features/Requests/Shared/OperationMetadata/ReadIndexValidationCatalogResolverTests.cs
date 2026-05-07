using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ReadIndexValidationCatalogResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenReadIndexDisabled_ReturnsSyntaxOnlySuccess ()
    {
        var persistedCatalog = CreatePersistedCatalogStub(IndexFreshness.Fresh);
        var loader = new SpyPersistedOpsCatalogReader(
            PersistedOpsCatalogReadResult.Success(
                persistedCatalog.Entries,
                persistedCatalog.GeneratedAtUtc,
                persistedCatalog.Freshness));
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
    public async Task Resolve_WhenAllowStaleAndPersistedCatalogIsMissing_ReturnsSyntaxOnlySuccess ()
    {
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogReader(
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
    public async Task Resolve_WhenRequireFreshAndPersistedCatalogIsStale_ReturnsFailureWithReadIndexHit ()
    {
        var persistedCatalog = CreatePersistedCatalogStub(IndexFreshness.Stale);
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogReader(
            PersistedOpsCatalogReadResult.Success(
                persistedCatalog.Entries,
                persistedCatalog.GeneratedAtUtc,
                persistedCatalog.Freshness)));

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
    public async Task Resolve_WhenPersistedCatalogEntryIsMalformed_ReturnsFormatInvalidFailure ()
    {
        var malformedPersistedCatalog = new
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
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogReader(
            PersistedOpsCatalogReadResult.Success(
                malformedPersistedCatalog.Entries,
                malformedPersistedCatalog.GeneratedAtUtc,
                malformedPersistedCatalog.Freshness)));

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
    public async Task Resolve_WhenPersistedCatalogIsUsable_ReturnsMetadataBackedSuccess ()
    {
        var persistedCatalog = CreatePersistedCatalogStub(IndexFreshness.Probable);
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogReader(
            PersistedOpsCatalogReadResult.Success(
                persistedCatalog.Entries,
                persistedCatalog.GeneratedAtUtc,
                persistedCatalog.Freshness)));

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

    private static PersistedOpsCatalogReadResultStub CreatePersistedCatalogStub (IndexFreshness freshness)
    {
        return new PersistedOpsCatalogReadResultStub(
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

    private sealed record PersistedOpsCatalogReadResultStub (
        IReadOnlyList<IndexOpEntryJsonContract> Entries,
        DateTimeOffset GeneratedAtUtc,
        IndexFreshness Freshness);

    private sealed class SpyPersistedOpsCatalogReader : IPersistedOpsCatalogReader
    {
        private readonly PersistedOpsCatalogReadResult result;

        public SpyPersistedOpsCatalogReader (PersistedOpsCatalogReadResult result)
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
