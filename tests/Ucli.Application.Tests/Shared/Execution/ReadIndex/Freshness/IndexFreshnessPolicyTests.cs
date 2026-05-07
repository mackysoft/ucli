using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexFreshnessPolicyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ApplyModeConstraint_ReturnsFailure_WhenRequireFreshAndFreshnessIsStale ()
    {
        var result = IndexFreshnessPolicy.ApplyModeConstraint(ReadIndexMode.RequireFresh, IndexFreshness.Stale);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFreshRequired, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ApplyModeConstraint_ReturnsSuccess_WhenAllowStaleAndFreshnessIsStale ()
    {
        var result = IndexFreshnessPolicy.ApplyModeConstraint(ReadIndexMode.AllowStale, IndexFreshness.Stale);

        Assert.True(result.IsSuccess);
        Assert.Equal(IndexFreshness.Stale, result.Freshness);
        Assert.Null(result.Error);
    }
}
