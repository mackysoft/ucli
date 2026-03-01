using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class PlanTokenModeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenMode_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)PlanTokenMode.Optional);
        Assert.Equal(1, (int)PlanTokenMode.Required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeValues_HasStableStringValues ()
    {
        Assert.Equal("optional", PlanTokenModeValues.Optional);
        Assert.Equal("required", PlanTokenModeValues.Required);
    }
}