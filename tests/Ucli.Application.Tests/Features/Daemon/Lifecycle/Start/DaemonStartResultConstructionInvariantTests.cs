using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartResultConstructionInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenDaemonStatusHasNoContractLiteral_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DaemonStartResult.Failure(
            ExecutionError.InternalError("failure"),
            daemonStatus: (DaemonStatusKind)int.MaxValue));
    }

}
