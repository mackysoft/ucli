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

        var result = await service.GetAll(new OpsCommandInput(null, NormalizeMode(null), NormalizeTimeout(null), null));

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid readIndexMode", result.Message);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(0, catalogAccessService.CallCount);
        Assert.Equal(0, listResultMapper.CallCount);
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
        var catalogOutput = new OpsCatalogReadOutput(
            Snapshot: CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    CreateSceneSaveEntry(),
                ]),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        var catalogAccessService = new StubOpsCatalogAccessService
        {
            Result = OpsCatalogReadResult.Success(catalogOutput, "read ok"),
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

        var result = await service.GetAll(new OpsCommandInput("/repo", NormalizeMode("auto"), NormalizeTimeout("1000"), NormalizeReadIndexMode("allowStale"), true));

        Assert.Same(expectedResult, result);
        Assert.NotNull(preflightService.LastInput);
        Assert.True(preflightService.LastInput!.FailFast);
        Assert.Equal(1, catalogAccessService.CallCount);
        Assert.Equal(1, listResultMapper.CallCount);
        Assert.Same(catalogOutput, listResultMapper.LastOutput);
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
        var catalogOutput = new OpsCatalogReadOutput(
            Snapshot: CreateSnapshot(
                DateTimeOffset.UtcNow,
                Array.Empty<IndexOpEntryJsonContract>()),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        var catalogAccessService = new StubOpsCatalogAccessService
        {
            Result = OpsCatalogReadResult.Success(catalogOutput, "read ok"),
        };
        var expectedResult = OpsDescribeServiceResult.Failure("missing", UcliCoreErrorCodes.InvalidArgument);
        var listResultMapper = new StubOpsListResultMapper();
        var describeResultMapper = new StubOpsDescribeResultMapper
        {
            Result = expectedResult,
        };
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.Describe(
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
        Assert.Equal("ucli.unknown", describeResultMapper.LastOperationName);
    }

    private sealed class StubOpsPreflightService : IOpsPreflightService
    {
        public OpsPreflightResult Result { get; set; } = OpsPreflightResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public OpsCommandInput? LastInput { get; private set; }

        public ValueTask<OpsPreflightResult> Execute (
            OpsCommandInput input,
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

        public OpsCatalogReadResult Result { get; set; } = OpsCatalogReadResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public ValueTask<OpsCatalogReadResult> Read (
            OpsPreflightContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubOpsListResultMapper : IOpsListResultMapper
    {
        public int CallCount { get; private set; }

        public OpsCatalogReadOutput? LastOutput { get; private set; }

        public OpsListServiceResult Result { get; set; } = OpsListServiceResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public OpsListServiceResult Map (OpsCatalogReadOutput output)
        {
            CallCount++;
            LastOutput = output;
            return Result;
        }
    }

    private sealed class StubOpsDescribeResultMapper : IOpsDescribeResultMapper
    {
        public int CallCount { get; private set; }

        public OpsCatalogReadOutput? LastOutput { get; private set; }

        public string? LastOperationName { get; private set; }

        public OpsDescribeServiceResult Result { get; set; } = OpsDescribeServiceResult.Failure("not configured", UcliCoreErrorCodes.InternalError);

        public OpsDescribeServiceResult Map (
            OpsCatalogReadOutput output,
            string? operationName)
        {
            CallCount++;
            LastOutput = output;
            LastOperationName = operationName;
            return Result;
        }
    }

}
