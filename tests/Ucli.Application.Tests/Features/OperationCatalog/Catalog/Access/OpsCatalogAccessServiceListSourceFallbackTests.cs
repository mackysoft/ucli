using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Ops.Access.OpsCatalogAccessServiceTestSupport;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceListSourceFallbackTests
{
    private static readonly SourceFallbackFailureCase[] SourceFallbackFailures =
    [
        new(
            PersistedOpsCatalogReadFailureKind.Unavailable,
            ReadIndexErrorCodes.ReadIndexBootstrapFailed,
            "Index contract file was not found: ops.catalog.json."),
        new(
            PersistedOpsCatalogReadFailureKind.Malformed,
            ReadIndexErrorCodes.ReadIndexFormatInvalid,
            "Index contract file 'ops.catalog.json' is malformed."),
        new(
            PersistedOpsCatalogReadFailureKind.FreshnessUnavailable,
            ReadIndexErrorCodes.ReadIndexFreshRequired,
            "readIndex freshness could not be observed."),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var persistedReader = new RecordingPersistedOpsCatalogReader
        {
            ReadResult = CreatePersistedReadResult(
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Stale,
                [CreateGoDescribeEntry()]),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateSceneSaveEntry()], "Existing ops index freshness is 'stale'."),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.RequireFresh);

        var result = await service.ReadListAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.False(result.Output.AccessInfo.Used);
        Assert.Equal(IndexFreshness.Fresh, result.Output.AccessInfo.Freshness);
        Assert.Equal(generatedAtUtc, result.Output.AccessInfo.GeneratedAtUtc);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflight(
            sourceRefreshService,
            context,
            "Existing ops index freshness is 'stale'.",
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenPersistedReadReturnsFallbackFailure_FallsBackToSource ()
    {
        foreach (var testCase in SourceFallbackFailures)
        {
            var persistedReader = new RecordingPersistedOpsCatalogReader
            {
                ReadResult = PersistedOpsCatalogReadResult.Failure(
                    new PersistedOpsCatalogReadFailure(
                        testCase.FailureKind,
                        testCase.ErrorCode,
                        testCase.FailureMessage)),
            };
            var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
            var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
            {
                Result = CreateSourceRefreshResult(generatedAtUtc, [CreateSceneSaveEntry()], testCase.FailureMessage),
            };
            var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);
            var context = CreatePreflightContext(ReadIndexMode.RequireFresh);

            var result = await service.ReadListAsync(context, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
            Assert.False(result.Output.AccessInfo.Used);
            Assert.Equal(generatedAtUtc, result.Output.AccessInfo.GeneratedAtUtc);
            OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflight(
                sourceRefreshService,
                context,
                testCase.FailureMessage,
                CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenReadIndexDisabled_UsesSourceRefreshWithDisabledFallbackReason ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(generatedAtUtc, [CreateGoDescribeEntry()], "readIndex disabled by mode."),
        };
        var service = new OpsCatalogAccessService(new UnexpectedPersistedOpsCatalogReader(), sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.Disabled);

        var result = await service.ReadListAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflight(
            sourceRefreshService,
            context,
            "readIndex disabled by mode.",
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenSourceCatalogContainsDangerousOperation_ReturnsSourceCatalogAsProvided ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new RecordingOpsCatalogSourceRefreshService
        {
            Result = CreateSourceRefreshResult(
                generatedAtUtc,
                [CreateGoDescribeEntry(), CreateCsEvalEntry()],
                "readIndex disabled by mode."),
        };
        var service = new OpsCatalogAccessService(new UnexpectedPersistedOpsCatalogReader(), sourceRefreshService);
        var context = CreatePreflightContext(ReadIndexMode.Disabled);

        var result = await service.ReadListAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [UcliPrimitiveOperationNames.GoDescribe, UcliPrimitiveOperationNames.CsEval],
            result.Output!.Snapshot.Operations.Select(static operation => operation.Name));
        OpsCatalogAccessInvocationAssert.SourceRefreshedFromPreflight(
            sourceRefreshService,
            context,
            "readIndex disabled by mode.",
            CancellationToken.None);
    }

    private sealed record SourceFallbackFailureCase (
        PersistedOpsCatalogReadFailureKind FailureKind,
        UcliCode ErrorCode,
        string FailureMessage);
}
