namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class ConfigValueLiteralContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void OperationPolicyValues_HasStableStringValues ()
    {
        Assert.Equal("safe", "safe");
        Assert.Equal("advanced", "advanced");
        Assert.Equal("dangerous", "dangerous");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationExposureValues_HasStableStringValues ()
    {
        Assert.Equal("public", "public");
        Assert.Equal("editLoweringOnly", "editLoweringOnly");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexModeValues_HasStableStringValues ()
    {
        Assert.Equal("disabled", "disabled");
        Assert.Equal("allowStale", "allowStale");
        Assert.Equal("requireFresh", "requireFresh");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliOperationKindValues_HasStableStringValues ()
    {
        Assert.Equal("mutation", "mutation");
        Assert.Equal("query", "query");
        Assert.Equal("command", "command");
    }
}
