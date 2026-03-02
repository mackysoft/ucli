using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class ConfigValueLiteralContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void OperationPolicyValues_HasStableStringValues ()
    {
        Assert.Equal("safe", OperationPolicyValues.Safe);
        Assert.Equal("advanced", OperationPolicyValues.Advanced);
        Assert.Equal("dangerous", OperationPolicyValues.Dangerous);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexModeValues_HasStableStringValues ()
    {
        Assert.Equal("disabled", ReadIndexModeValues.Disabled);
        Assert.Equal("allowStale", ReadIndexModeValues.AllowStale);
        Assert.Equal("requireFresh", ReadIndexModeValues.RequireFresh);
    }
}