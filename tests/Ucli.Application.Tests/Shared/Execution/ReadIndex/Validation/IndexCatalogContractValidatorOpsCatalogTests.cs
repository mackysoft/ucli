using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsCatalogSnapshot_ReturnsTrue_WhenDescribeContractIsComplete ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("source-hash").ToString(),
            Entries:
            [
                IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsCatalogEntry(),
            ]);

        var result = OpsCatalogDescriptorSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsCatalogSnapshot_ReturnsFalse_WhenDescriptionIsMissing ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("source-hash").ToString(),
            Entries:
            [
                IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsCatalogEntry() with { Description = null },
            ]);

        var result = OpsCatalogDescriptorSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsCatalogSnapshot_ReturnsTrue_WhenEditLoweringOnlyPreviewStateIsAllowed ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateEditLoweringOnlyOpsEntry();

        var result = OpsCatalogSnapshot.TryCreate(
            DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            [entry],
            "operations",
            allowEditLoweringOnlyEntries: true,
            out var snapshot,
            out var error);

        Assert.True(result, error);
        Assert.NotNull(snapshot);
        Assert.Null(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsCatalogSnapshot_ReturnsFalse_WhenEditLoweringOnlyEntryIsNotAllowed ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateEditLoweringOnlyOpsEntry();

        var result = OpsCatalogSnapshot.TryCreate(
            DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            [entry],
            "operations",
            allowEditLoweringOnlyEntries: false,
            out var snapshot,
            out var error);

        Assert.False(result);
        Assert.Null(snapshot);
        Assert.NotNull(error);
    }

}
