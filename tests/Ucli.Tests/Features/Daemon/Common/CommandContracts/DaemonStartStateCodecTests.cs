using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStartStateCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryToValue_WhenStatusIsAttached_ReturnsAttachedContractValue ()
    {
        var success = DaemonStartStateCodec.TryToValue(DaemonStartStatus.Attached, out var value);

        Assert.True(success);
        Assert.Equal("attached", value);
    }
}
