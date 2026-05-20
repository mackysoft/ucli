using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Helpers.OperationCatalog.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ReadIndexValidationCatalogResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenReadIndexDisabled_ReturnsSyntaxOnlySuccess ()
    {
        var loader = new SpyPersistedOpsCatalogReader(
            CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry()]));
        var resolver = new ReadIndexValidationCatalogResolver(loader);

        var result = await resolver.ResolveAsync(
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
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Unavailable,
                    ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                    "Index contract file was not found: ops.catalog.json."))));

        var result = await resolver.ResolveAsync(
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
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogReader(
            CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Stale,
                [CreateGoDescribeEntry()])));

        var result = await resolver.ResolveAsync(
            CreateUnityProject(),
            ReadIndexMode.RequireFresh,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFreshRequired, result.ErrorCode);
        Assert.True(result.ReadIndex.Used);
        Assert.True(result.ReadIndex.Hit);
        Assert.Equal(IndexFreshness.Stale, result.ReadIndex.Freshness);
        Assert.Contains("requireFresh", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenPersistedCatalogEntryIsMalformed_ReturnsFormatInvalidFailure ()
    {
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogReader(
            PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Malformed,
                    ReadIndexErrorCodes.ReadIndexFormatInvalid,
                    "Index contract file 'ops.catalog.json' is malformed."))));

        var result = await resolver.ResolveAsync(
            CreateUnityProject(),
            ReadIndexMode.AllowStale,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.ErrorCode);
        Assert.False(result.ReadIndex.Used);
        Assert.False(result.ReadIndex.Hit);
        Assert.Contains("malformed", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenPersistedCatalogIsUsable_ReturnsMetadataBackedSuccess ()
    {
        var resolver = new ReadIndexValidationCatalogResolver(new SpyPersistedOpsCatalogReader(
            CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Probable,
                [CreateGoDescribeEntry()])));

        var result = await resolver.ResolveAsync(
            CreateUnityProject(),
            ReadIndexMode.AllowStale,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Catalog.IsAvailable);
        Assert.Contains(result.Catalog.Operations, operation => operation.Name == UcliPrimitiveOperationNames.GoDescribe);
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

    private sealed class SpyPersistedOpsCatalogReader : IPersistedOpsCatalogReader
    {
        private readonly PersistedOpsCatalogReadResult result;

        public SpyPersistedOpsCatalogReader (PersistedOpsCatalogReadResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public ValueTask<PersistedOpsCatalogReadResult> ReadAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }

        public ValueTask<PersistedOpsCatalogDescriptorReadResult> ReadDescriptorsAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<PersistedOpsDescribeReadResult> ReadDescribeAsync (
            ResolvedUnityProjectContext unityProject,
            OpsCatalogDescriptorSnapshot catalogSnapshot,
            IndexOpsCatalogEntryJsonContract catalogEntry,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
