using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops;

public sealed class OpsServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_WhenPreflightFails_ReturnsFailureWithoutReadingCatalog ()
    {
        var preflightService = new RecordingOpsPreflightService
        {
            Result = OpsPreflightResult.Failure("invalid readIndexMode", UcliCoreErrorCodes.InvalidArgument),
        };
        var catalogAccessService = new RecordingOpsCatalogAccessService();
        var listResultMapper = new RecordingOpsListResultMapper();
        var describeResultMapper = new RecordingOpsDescribeResultMapper();
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.GetAllAsync(new OpsCommandInput(null, NormalizeMode(null), NormalizeTimeout(null), null, null, null, null));

        OpsServiceInvocationAssert.ListPreflightFailureReturnedBeforeCatalogRead(
            result,
            catalogAccessService,
            listResultMapper,
            "invalid readIndexMode",
            UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_WhenNameRegexIsInvalid_ReturnsFailureWithoutReadingCatalog ()
    {
        var preflightService = new RecordingOpsPreflightService();
        var catalogAccessService = new RecordingOpsCatalogAccessService();
        var listResultMapper = new RecordingOpsListResultMapper();
        var describeResultMapper = new RecordingOpsDescribeResultMapper();
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.GetAllAsync(new OpsCommandInput(null, null, null, null, "[", null, null));

        OpsServiceInvocationAssert.InvalidListFilterRejectedBeforePreflight(
            result,
            preflightService,
            catalogAccessService,
            listResultMapper,
            UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_WhenCatalogReadSucceeds_ReturnsMappedFilteredOperations ()
    {
        var preflightContext = new OpsPreflightContext(default!, default, UnityExecutionMode.Auto, TimeSpan.FromMilliseconds(1000), false);
        var preflightService = new RecordingOpsPreflightService
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
        var catalogAccessService = new RecordingOpsCatalogAccessService
        {
            ListResult = OpsListReadResult.Success(catalogOutput, "read ok"),
        };
        var expectedResult = OpsListServiceResult.Success(
            new OpsListExecutionOutput(
                Operations:
                [
                    new OpsOperationListItem(
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                        "mutation",
                        "advanced",
                        "Saves a Unity scene asset."),
                ],
                ReadIndex: new ReadIndexInfo(
                    true,
                    true,
                    ReadIndexInfoSource.Index,
                    IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            "mapped");
        var listResultMapper = new RecordingOpsListResultMapper
        {
            Result = expectedResult,
        };
        var describeResultMapper = new RecordingOpsDescribeResultMapper();
        var service = new OpsService(preflightService, catalogAccessService, listResultMapper, describeResultMapper);

        var result = await service.GetAllAsync(new OpsCommandInput("/repo", NormalizeMode("auto"), NormalizeTimeout("1000"), NormalizeReadIndexMode("allowStale"), null, null, null, true));

        Assert.Same(expectedResult, result);
        OpsServiceInvocationAssert.PreflightRequestedFailFast(preflightService);
        OpsServiceInvocationAssert.CatalogListReadFromPreflight(catalogAccessService, preflightContext);
        OpsServiceInvocationAssert.ListMappedFrom(
            listResultMapper,
            catalogOutput,
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Describe_WhenCatalogReadSucceeds_ReturnsMappedOperationDetail ()
    {
        var preflightContext = new OpsPreflightContext(default!, default, UnityExecutionMode.Auto, TimeSpan.FromMilliseconds(1000), false);
        var preflightService = new RecordingOpsPreflightService
        {
            Result = OpsPreflightResult.Success(preflightContext),
        };
        var catalogOutput = new OpsDescribeReadOutput(
            Operation: OperationCatalogTestFixtures.CreateValidatedOperation(CreateGoDescribeEntry()),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        var catalogAccessService = new RecordingOpsCatalogAccessService
        {
            DescribeResult = OpsDescribeReadResult.Success(catalogOutput, "read ok"),
        };
        var expectedResult = OpsDescribeServiceResult.Failure("missing", UcliCoreErrorCodes.InvalidArgument);
        var listResultMapper = new RecordingOpsListResultMapper();
        var describeResultMapper = new RecordingOpsDescribeResultMapper
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
        OpsServiceInvocationAssert.PreflightRequestedFailFast(preflightService);
        OpsServiceInvocationAssert.CatalogDescribeReadFromPreflight(
            catalogAccessService,
            preflightContext,
            "ucli.unknown");
        OpsServiceInvocationAssert.DescribeMappedFrom(describeResultMapper, catalogOutput);
    }
}
