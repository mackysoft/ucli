using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Helpers.OperationCatalog.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceTests
{
    public static TheoryData<string, UcliErrorCode, string> SourceFallbackFailures =>
        new()
        {
            {
                nameof(PersistedOpsCatalogReadFailureKind.Unavailable),
                ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: ops.catalog.json."
            },
            {
                nameof(PersistedOpsCatalogReadFailureKind.Malformed),
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'ops.catalog.json' is malformed."
            },
            {
                nameof(PersistedOpsCatalogReadFailureKind.FreshnessUnavailable),
                ReadIndexErrorCodes.ReadIndexFreshRequired,
                "readIndex freshness could not be observed."
            },
        };

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAllowStaleIndexExists_ReturnsPersistedCatalog ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Probable,
                [CreateGoDescribeEntry()]),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.AllowStale), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Index, result.Output!.AccessInfo.Source);
        Assert.True(result.Output.AccessInfo.Used);
        Assert.True(result.Output.AccessInfo.Hit);
        Assert.Equal(IndexFreshness.Probable, result.Output.AccessInfo.Freshness);
        Assert.Equal(0, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Stale,
                [CreateGoDescribeEntry()]),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateSceneSaveEntry()], "Existing ops index freshness is 'stale'."),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.RequireFresh), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.False(result.Output.AccessInfo.Used);
        Assert.Equal(IndexFreshness.Fresh, result.Output.AccessInfo.Freshness);
        Assert.Equal(generatedAtUtc, result.Output.AccessInfo.GeneratedAtUtc);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, sourceRefreshService.CallCount);
        Assert.Equal("Existing ops index freshness is 'stale'.", sourceRefreshService.LastFallbackReason);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SourceFallbackFailures))]
    public async Task Read_WhenPersistedReadReturnsFallbackFailure_FallsBackToSource (
        string failureKindName,
        UcliErrorCode errorCode,
        string failureMessage)
    {
        var failureKind = Enum.Parse<PersistedOpsCatalogReadFailureKind>(failureKindName);
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    failureKind,
                    errorCode,
                    failureMessage)),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateSceneSaveEntry()], failureMessage),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.RequireFresh), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.False(result.Output.AccessInfo.Used);
        Assert.Equal(generatedAtUtc, result.Output.AccessInfo.GeneratedAtUtc);
        Assert.Equal(failureMessage, sourceRefreshService.LastFallbackReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenPersistedReadReturnsInvalidArgument_ReturnsFailureWithoutSourceFallback ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.InvalidArgument,
                    UcliCoreErrorCodes.InvalidArgument,
                    "invalid project fingerprint")),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.AllowStale), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(0, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenReadIndexDisabled_UsesSourceRefreshWithDisabledFallbackReason ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "readIndex disabled by mode."),
        };
        var service = new OpsCatalogAccessService(new StubPersistedOpsCatalogReader(), sourceRefreshService);

        var result = await service.ReadListAsync(CreatePreflightContext(ReadIndexMode.Disabled), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal("readIndex disabled by mode.", sourceRefreshService.LastFallbackReason);
        Assert.Equal(1, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenIndexHit_ReadsOnlyRequestedDetail ()
    {
        var sceneSave = CreateSceneSaveEntry();
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry(), sceneSave]),
            DescribeResult = PersistedOpsDescribeReadResult.Success(sceneSave),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.RequireFresh),
            sceneSave.Name,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(sceneSave, result.Output!.Operation);
        Assert.Equal(1, persistedReader.ReadDescribeCallCount);
        Assert.Equal(sceneSave.Name, persistedReader.LastDescribeCatalogEntry!.Name);
        Assert.Equal(0, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenDetailArtifactIsBroken_FallsBackToSource ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry()]),
            DescribeResult = PersistedOpsDescribeReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Malformed,
                    ReadIndexErrorCodes.ReadIndexFormatInvalid,
                    "Index contract file 'catalogs/ops.describe/<opKey>.json' is malformed.")),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "detail broken"),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.RequireFresh),
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal(1, sourceRefreshService.CallCount);
        Assert.Contains("ops.describe", sourceRefreshService.LastFallbackReason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenReadIndexDisabled_UsesSourceRefreshWithDisabledFallbackReason ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "readIndex disabled by mode."),
        };
        var service = new OpsCatalogAccessService(new StubPersistedOpsCatalogReader(), sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.Disabled),
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal("readIndex disabled by mode.", sourceRefreshService.LastFallbackReason);
        Assert.Equal(1, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Stale,
                [CreateGoDescribeEntry()]),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "Existing ops index freshness is 'stale'."),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.RequireFresh),
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, sourceRefreshService.CallCount);
        Assert.Equal("Existing ops index freshness is 'stale'.", sourceRefreshService.LastFallbackReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenPersistedReadReturnsInvalidArgument_ReturnsFailureWithoutSourceFallback ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.InvalidArgument,
                    UcliCoreErrorCodes.InvalidArgument,
                    "invalid project fingerprint")),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.AllowStale),
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(0, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenOperationMissingFromDescriptor_ReturnsInvalidArgumentWithoutSourceFallback ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Fresh,
                [CreateGoDescribeEntry()]),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.RequireFresh),
            UcliPrimitiveOperationNames.SceneSave,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(0, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadDescribe_WhenSourceRefreshFails_ReturnsSourceFailure ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Unavailable,
                    ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                    "Index contract file was not found: ops.catalog.json.")),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = OpsCatalogSourceRefreshResult.Failure("source refresh failed", UcliCoreErrorCodes.InternalError),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.ReadDescribeAsync(
            CreatePreflightContext(ReadIndexMode.RequireFresh),
            UcliPrimitiveOperationNames.GoDescribe,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(1, sourceRefreshService.CallCount);
    }


    private static OpsPreflightContext CreatePreflightContext (ReadIndexMode readIndexMode)
    {
        return new OpsPreflightContext(
            new ProjectContext(
                new ResolvedUnityProjectContext(
                    UnityProjectRoot: "/repo/UnityProject",
                    RepositoryRoot: "/repo",
                    ProjectFingerprint: "project-fingerprint",
                    PathSource: UnityProjectPathSource.CommandOption),
                UcliConfig.CreateDefault(),
                ConfigSource.Default),
            readIndexMode,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            true);
    }

    private sealed class StubPersistedOpsCatalogReader : IPersistedOpsCatalogReader
    {
        public PersistedOpsCatalogReadResult Result { get; set; }
            = PersistedOpsCatalogReadResult.Failure(
                new PersistedOpsCatalogReadFailure(
                    PersistedOpsCatalogReadFailureKind.Unavailable,
                    ReadIndexErrorCodes.ReadIndexBootstrapFailed,
                    "Index contract file was not found: ops.catalog.json."));

        public PersistedOpsDescribeReadResult? DescribeResult { get; set; }

        public int ReadDescribeCallCount { get; private set; }

        public IndexOpsCatalogEntryJsonContract? LastDescribeCatalogEntry { get; private set; }

        public ValueTask<PersistedOpsCatalogReadResult> ReadAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }

        public ValueTask<PersistedOpsCatalogDescriptorReadResult> ReadDescriptorsAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            cancellationToken.ThrowIfCancellationRequested();

            if (!Result.IsSuccess)
            {
                return ValueTask.FromResult(PersistedOpsCatalogDescriptorReadResult.Failure(Result.ReadFailure!));
            }

            var entries = Result.Snapshot!.Operations
                .Select(static (operation, index) => new IndexOpsCatalogEntryJsonContract(
                    operation.Name,
                    operation.Kind,
                    operation.Policy,
                    operation.Description,
                    new string((char)('a' + index), 64),
                    new string((char)('1' + index), 64)))
                .ToArray();
            Assert.True(OpsCatalogDescriptorSnapshot.TryCreate(
                Result.Snapshot.GeneratedAtUtc,
                "source-hash",
                entries,
                "entries",
                out var snapshot,
                out var error));
            Assert.Null(error);
            return ValueTask.FromResult(PersistedOpsCatalogDescriptorReadResult.Success(snapshot!, Result.Freshness!.Value));
        }

        public ValueTask<PersistedOpsDescribeReadResult> ReadDescribeAsync (
            ResolvedUnityProjectContext unityProject,
            OpsCatalogDescriptorSnapshot catalogSnapshot,
            IndexOpsCatalogEntryJsonContract catalogEntry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            ArgumentNullException.ThrowIfNull(catalogSnapshot);
            ArgumentNullException.ThrowIfNull(catalogEntry);
            cancellationToken.ThrowIfCancellationRequested();
            ReadDescribeCallCount++;
            LastDescribeCatalogEntry = catalogEntry;
            if (DescribeResult != null)
            {
                return ValueTask.FromResult(DescribeResult);
            }

            var operation = Result.Snapshot!.Operations.First(operation => string.Equals(operation.Name, catalogEntry.Name, StringComparison.Ordinal));
            return ValueTask.FromResult(PersistedOpsDescribeReadResult.Success(operation));
        }
    }

    private sealed class StubOpsCatalogSourceRefreshService : IOpsCatalogSourceRefreshService
    {
        public int CallCount { get; private set; }

        public string? LastFallbackReason { get; private set; }

        public OpsCatalogSourceRefreshResult Result { get; set; }
            = OpsCatalogSourceRefreshResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public ValueTask<OpsCatalogSourceRefreshResult> RefreshAsync (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UnityExecutionMode mode,
            TimeSpan timeout,
            bool failFast,
            string fallbackReason,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(config);
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastFallbackReason = fallbackReason;
            return ValueTask.FromResult(Result);
        }
    }
}
