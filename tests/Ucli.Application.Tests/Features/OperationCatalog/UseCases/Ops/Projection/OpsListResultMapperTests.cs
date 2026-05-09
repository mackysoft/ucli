using System.Text.RegularExpressions;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Filtering;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.Helpers.OperationCatalog.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Mapping;

public sealed class OpsListResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_SortsOperationsByName ()
    {
        var mapper = new OpsListResultMapper(new OpsReadIndexInfoMapper());

        var result = mapper.Map(
            new OpsListReadOutput(
                Snapshot: OpsCatalogListSnapshot.FromCatalog(CreateSnapshot(
                    DateTimeOffset.UtcNow,
                    [
                        CreateSceneSaveEntry(),
                        CreateGoDescribeEntry(),
                    ])),
                AccessInfo: new OpsCatalogAccessInfo(
                    true,
                    true,
                    OpsCatalogSource.Index,
                    MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                    DateTimeOffset.UtcNow,
                    null)),
            new OpsListFilter(null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, result.Output!.Operations[0].Name);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, result.Output.Operations[1].Name);
        Assert.Equal(ReadIndexInfoSource.Index, result.Output.ReadIndex.Source);
        Assert.Equal(MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh, result.Output.ReadIndex.Freshness);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_AppliesNameRegexKindAndMaxPolicyAsAndFilters ()
    {
        var mapper = new OpsListResultMapper(new OpsReadIndexInfoMapper());
        var output = new OpsListReadOutput(
            Snapshot: OpsCatalogListSnapshot.FromCatalog(CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    CreateGoDescribeEntry(),
                    CreateSceneSaveEntry(),
                ])),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        Assert.True(OpsListFilter.TryCreate(
            new OpsCommandInput(
                ProjectPath: null,
                Mode: null,
                TimeoutMilliseconds: null,
                ReadIndexMode: null,
                NameRegex: "scene",
                Kind: UcliOperationKind.Mutation,
                MaxPolicy: OperationPolicy.Advanced),
            out var filter,
            out _));

        var result = mapper.Map(output, filter!);

        Assert.True(result.IsSuccess);
        var operation = Assert.Single(result.Output!.Operations);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, operation.Name);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(OperationPolicy.Safe, 1)]
    [InlineData(OperationPolicy.Advanced, 2)]
    [InlineData(OperationPolicy.Dangerous, 2)]
    public void Map_AppliesMaxPolicyAsUpperBound (
        OperationPolicy maxPolicy,
        int expectedCount)
    {
        var mapper = new OpsListResultMapper(new OpsReadIndexInfoMapper());
        var output = new OpsListReadOutput(
            Snapshot: OpsCatalogListSnapshot.FromCatalog(CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    CreateGoDescribeEntry(),
                    CreateSceneSaveEntry(),
                ])),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        var filter = new OpsListFilter(null, null, maxPolicy);

        var result = mapper.Map(output, filter);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCount, result.Output!.Operations.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenNameRegexTimesOut_ReturnsInvalidArgument ()
    {
        var mapper = new OpsListResultMapper(new OpsReadIndexInfoMapper());
        var output = new OpsListReadOutput(
            Snapshot: OpsCatalogListSnapshot.FromCatalog(CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    CreateGoDescribeEntry() with { Name = new string('a', 50_000) + "!" },
                ])),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
        var filter = new OpsListFilter(
            new Regex("^(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromTicks(1)),
            null,
            null);

        var result = mapper.Map(output, filter);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Contains("nameRegex", result.Message, StringComparison.Ordinal);
    }
}
