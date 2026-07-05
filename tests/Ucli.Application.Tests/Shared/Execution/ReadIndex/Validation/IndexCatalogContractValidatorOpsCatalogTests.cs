namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsTrue_WhenDescribeContractIsComplete ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsCatalogEntry(),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenDescriptionIsMissing ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsCatalogEntry() with { Description = null },
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateOpsEntries_ReturnsTrue_WhenEditLoweringOnlyPreviewStateIsAllowed ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateEditLoweringOnlyOpsEntry();

        var result = IndexCatalogContractValidator.TryValidateOpsEntries(
            [entry],
            "operations",
            allowEditLoweringOnlyEntries: true,
            out var error);

        Assert.True(result, error);
        Assert.Null(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateOpsEntries_ReturnsFalse_WhenEditLoweringOnlyEntryIsNotAllowed ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateEditLoweringOnlyOpsEntry();

        var result = IndexCatalogContractValidator.TryValidateOpsEntries(
            [entry],
            "operations",
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateOpsEntries_ReturnsFalse_WhenUnknownExposureEntryIsIncluded ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateEditLoweringOnlyOpsEntry() with
        {
            Exposure = "diagnosticOnly",
        };

        var result = IndexCatalogContractValidator.TryValidateOpsEntries(
            [entry],
            "operations",
            allowEditLoweringOnlyEntries: true,
            out var error);

        Assert.False(result);
        Assert.NotNull(error);
    }
}
