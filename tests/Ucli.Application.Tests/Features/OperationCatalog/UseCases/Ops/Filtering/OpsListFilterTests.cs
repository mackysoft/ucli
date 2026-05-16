using System.Text.RegularExpressions;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Filtering;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.Helpers.OperationCatalog.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests.Ops.Filtering;

public sealed class OpsListFilterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Apply_AppliesNameRegexKindAndMaxPolicyAsAndFilters ()
    {
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
        var snapshot = CreateListSnapshot(
            CreateSceneSaveEntry(),
            CreateSceneSaveEntry() with { Name = "custom.scene.query", Kind = "query", Policy = "safe", Assurance = CreateGoDescribeEntry().Assurance },
            CreateSceneSaveEntry() with { Name = "custom.asset.mutation", Kind = "mutation", Policy = "safe" },
            CreateSceneSaveEntry() with { Name = "custom.scene.dangerous", Kind = "mutation", Policy = "dangerous" });

        var result = filter!.Apply(snapshot.Operations);

        Assert.True(result.IsSuccess);
        var operation = Assert.Single(result.Operations!);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, operation.Name);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(OperationPolicy.Safe, 1)]
    [InlineData(OperationPolicy.Advanced, 2)]
    [InlineData(OperationPolicy.Dangerous, 2)]
    public void Apply_AppliesMaxPolicyAsUpperBound (
        OperationPolicy maxPolicy,
        int expectedCount)
    {
        var snapshot = CreateListSnapshot(
            CreateGoDescribeEntry(),
            CreateSceneSaveEntry());
        var filter = new OpsListFilter(null, null, maxPolicy);

        var result = filter.Apply(snapshot.Operations);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCount, result.Operations!.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Apply_WhenNameRegexTimesOut_ReturnsFailure ()
    {
        var snapshot = CreateListSnapshot(
            CreateGoDescribeEntry() with { Name = new string('a', 50_000) + "!" });
        var filter = new OpsListFilter(
            new Regex("^(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromTicks(1)),
            null,
            null);

        var result = filter.Apply(snapshot.Operations);

        Assert.False(result.IsSuccess);
        Assert.Contains("nameRegex", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static OpsCatalogListSnapshot CreateListSnapshot (params IndexOpEntryJsonContract[] operations)
    {
        return OpsCatalogListSnapshotFactory.FromCatalog(CreateSnapshot(DateTimeOffset.UtcNow, operations));
    }
}
