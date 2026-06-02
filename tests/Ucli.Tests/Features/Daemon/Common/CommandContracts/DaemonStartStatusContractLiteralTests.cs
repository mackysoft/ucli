namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStartStatusContractLiteralTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenStatusIsAttached_ReturnsAttachedContractValue ()
    {
        Assert.Equal("attached", ContractLiteralCodec.ToValue(DaemonStartStatus.Attached));
    }
}
