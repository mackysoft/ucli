using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class ConfigModeEnumContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void OperationPolicy_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)OperationPolicy.Safe);
        Assert.Equal(1, (int)OperationPolicy.Advanced);
        Assert.Equal(2, (int)OperationPolicy.Dangerous);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexMode_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)ReadIndexMode.Disabled);
        Assert.Equal(1, (int)ReadIndexMode.AllowStale);
        Assert.Equal(2, (int)ReadIndexMode.RequireFresh);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationKind_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)UcliOperationKind.Query);
        Assert.Equal(1, (int)UcliOperationKind.Mutation);
        Assert.Equal(2, (int)UcliOperationKind.Command);
    }
}
