using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class PlanTokenModeEnumContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenMode_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)PlanTokenMode.Optional);
        Assert.Equal(1, (int)PlanTokenMode.Required);
    }
}
