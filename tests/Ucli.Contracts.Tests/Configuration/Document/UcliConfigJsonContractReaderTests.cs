using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class UcliConfigJsonContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryReadPlanTokenLoose_WithInvalidPlanTokenType_ReturnsNullPlanTokenMode ()
    {
        const string json = """
        {
          "planTokenMode": 1,
          "operationPolicy": " safe ",
          "operationAllowlist": ["^foo\\.", " ", "^bar\\."]
        }
        """;

        using var document = JsonDocument.Parse(json);
        var result = UcliConfigJsonContractReader.TryReadPlanTokenLoose(
            document.RootElement,
            out var parsed);

        Assert.True(result);
        Assert.Null(parsed.PlanTokenMode);
        Assert.Equal("safe", parsed.OperationPolicy);
        Assert.NotNull(parsed.OperationAllowlist);
        Assert.Equal(["^foo\\.", "^bar\\."], parsed.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadPlanTokenLoose_WithInvalidAllowlistElement_ReturnsNullAllowlist ()
    {
        const string json = """
        {
          "planTokenMode": "required",
          "operationPolicy": "safe",
          "operationAllowlist": ["^foo\\.", 1]
        }
        """;

        using var document = JsonDocument.Parse(json);
        var result = UcliConfigJsonContractReader.TryReadPlanTokenLoose(
            document.RootElement,
            out var parsed);

        Assert.True(result);
        Assert.Null(parsed.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadPlanTokenLoose_WithNonObjectRoot_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse("[]");

        var result = UcliConfigJsonContractReader.TryReadPlanTokenLoose(
            document.RootElement,
            out var parsed);

        Assert.False(result);
        Assert.Equal(default, parsed);
    }
}
