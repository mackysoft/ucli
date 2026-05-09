using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;
using static MackySoft.Ucli.Application.Tests.Helpers.OperationCatalog.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops;

public sealed class OpsServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_WhenPreflightFails_ReturnsFailureWithoutReadingCatalog ()
    {
        var preflightService = new StubOpsPreflightService
        {
            Result = OpsPreflightResult.Failure("invalid readIndexMode", UcliCoreErrorCodes.InvalidArgument),
        };
        var catalogAccessService = new StubOpsCatalogAccessService();
        var listResultMapper = new StubOpsListResultMapper();
        var describeResultMapper = new StubOpsDescribeResultMapper();
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.GetAllAsync(new OpsCommandInput(null, NormalizeMode(null), NormalizeTimeout(null), null, null, null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid readIndexMode", result.Message);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(0, catalogAccessService.CallCount);
        Assert.Equal(0, listResultMapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_WhenNameRegexIsInvalid_ReturnsFailureWithoutReadingCatalog ()
    {
        var preflightService = new StubOpsPreflightService();
        var catalogAccessService = new StubOpsCatalogAccessService();
        var listResultMapper = new StubOpsListResultMapper();
        var describeResultMapper = new StubOpsDescribeResultMapper();
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.GetAllAsync(new OpsCommandInput(null, null, null, null, "[", null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(0, catalogAccessService.CallCount);
        Assert.Equal(0, listResultMapper.CallCount);
        Assert.Null(preflightService.LastInput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_WhenCatalogReadSucceeds_UsesListResultMapper ()
    {
        var preflightContext = new OpsPreflightContext(default!, default, UnityExecutionMode.Auto, TimeSpan.FromMilliseconds(1000), false);
        var preflightService = new StubOpsPreflightService
        {
            Result = OpsPreflightResult.Success(preflightContext),
        };
        var catalogOutput = new OpsListReadOutput(
            Snapshot: OpsCatalogListSnapshotFactory.FromCatalog(CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    CreateSceneSaveEntry(),
                ])),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        var catalogAccessService = new StubOpsCatalogAccessService
        {
            ListResult = OpsListReadResult.Success(catalogOutput, "read ok"),
        };
        var expectedResult = OpsListServiceResult.Success(
            new OpsListExecutionOutput(
                Operations:
                [
                    new OpsOperationListItem(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, "mutation", "advanced"),
                ],
                ReadIndex: new ReadIndexInfo(
                    true,
                    true,
                    ReadIndexInfoSource.Index,
                    IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            "mapped");
        var listResultMapper = new StubOpsListResultMapper
        {
            Result = expectedResult,
        };
        var describeResultMapper = new StubOpsDescribeResultMapper();
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.GetAllAsync(new OpsCommandInput("/repo", NormalizeMode("auto"), NormalizeTimeout("1000"), NormalizeReadIndexMode("allowStale"), null, null, null, true));

        Assert.Same(expectedResult, result);
        Assert.NotNull(preflightService.LastInput);
        Assert.True(preflightService.LastInput!.FailFast);
        Assert.Equal(1, catalogAccessService.CallCount);
        Assert.Equal(1, listResultMapper.CallCount);
        Assert.Same(catalogOutput, listResultMapper.LastOutput);
        var operation = Assert.Single(listResultMapper.LastOperations!);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, operation.Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Describe_WhenCatalogReadSucceeds_UsesDescribeResultMapper ()
    {
        var preflightContext = new OpsPreflightContext(default!, default, UnityExecutionMode.Auto, TimeSpan.FromMilliseconds(1000), false);
        var preflightService = new StubOpsPreflightService
        {
            Result = OpsPreflightResult.Success(preflightContext),
        };
        var catalogOutput = new OpsDescribeReadOutput(
            Operation: CreateGoDescribeEntry(),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        var catalogAccessService = new StubOpsCatalogAccessService
        {
            DescribeResult = OpsDescribeReadResult.Success(catalogOutput, "read ok"),
        };
        var expectedResult = OpsDescribeServiceResult.Failure("missing", UcliCoreErrorCodes.InvalidArgument);
        var listResultMapper = new StubOpsListResultMapper();
        var describeResultMapper = new StubOpsDescribeResultMapper
        {
            Result = expectedResult,
        };
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.DescribeAsync(
            new OpsDescribeCommandInput(
                OperationName: "ucli.unknown",
                ProjectPath: "/repo",
                Mode: NormalizeMode("auto"),
                TimeoutMilliseconds: NormalizeTimeout("1000"),
                ReadIndexMode: NormalizeReadIndexMode("allowStale"),
                FailFast: true));

        Assert.Same(expectedResult, result);
        Assert.NotNull(preflightService.LastInput);
        Assert.True(preflightService.LastInput!.FailFast);
        Assert.Equal(1, describeResultMapper.CallCount);
        Assert.Same(catalogOutput, describeResultMapper.LastOutput);
    }

    private sealed class StubOpsPreflightService : IOpsPreflightService
    {
        public OpsPreflightResult Result { get; set; } = OpsPreflightResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public OpsPreflightInput? LastInput { get; private set; }

        public ValueTask<OpsPreflightResult> ExecuteAsync (
            OpsPreflightInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastInput = input;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubOpsCatalogAccessService : IOpsCatalogAccessService
    {
        public int CallCount { get; private set; }

        public OpsListReadResult ListResult { get; set; } = OpsListReadResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public OpsDescribeReadResult DescribeResult { get; set; } = OpsDescribeReadResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public ValueTask<OpsListReadResult> ReadListAsync (
            OpsPreflightContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(ListResult);
        }

        public ValueTask<OpsDescribeReadResult> ReadDescribeAsync (
            OpsPreflightContext context,
            string? operationName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(DescribeResult);
        }
    }

    private sealed class StubOpsListResultMapper : IOpsListResultMapper
    {
        public int CallCount { get; private set; }

        public OpsListReadOutput? LastOutput { get; private set; }

        public IReadOnlyList<OpsCatalogListEntry>? LastOperations { get; private set; }

        public OpsListServiceResult Result { get; set; } = OpsListServiceResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public OpsListServiceResult Map (
            OpsListReadOutput output,
            IReadOnlyList<OpsCatalogListEntry> operations)
        {
            CallCount++;
            LastOutput = output;
            LastOperations = operations;
            return Result;
        }
    }

    private sealed class StubOpsDescribeResultMapper : IOpsDescribeResultMapper
    {
        public int CallCount { get; private set; }

        public OpsDescribeReadOutput? LastOutput { get; private set; }

        public OpsDescribeServiceResult Result { get; set; } = OpsDescribeServiceResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public OpsDescribeServiceResult Map (OpsDescribeReadOutput output)
        {
            CallCount++;
            LastOutput = output;
            return Result;
        }
    }

}
