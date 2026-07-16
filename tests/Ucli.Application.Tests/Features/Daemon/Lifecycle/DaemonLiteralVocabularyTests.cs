using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

namespace MackySoft.Ucli.Application.Tests.Features.Daemon.Lifecycle;

public sealed class DaemonLiteralVocabularyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void DaemonLiteralEnums_ContainOnlyWireVocabulary ()
    {
        Assert.Equal(
            [nameof(DaemonStatusKind.Running), nameof(DaemonStatusKind.NotRunning), nameof(DaemonStatusKind.Stale)],
            Enum.GetNames<DaemonStatusKind>());
        Assert.Equal(
            ["running", "notRunning", "stale"],
            ContractLiteralCodec.GetLiterals<DaemonStatusKind>());

        Assert.Equal(
            [nameof(DaemonStopStatus.Stopped), nameof(DaemonStopStatus.NotRunning)],
            Enum.GetNames<DaemonStopStatus>());
        Assert.Equal(
            ["stopped", "notRunning"],
            ContractLiteralCodec.GetLiterals<DaemonStopStatus>());

        Assert.Equal(
            [nameof(DaemonCleanupStatus.Completed), nameof(DaemonCleanupStatus.Skipped)],
            Enum.GetNames<DaemonCleanupStatus>());
        Assert.Equal(
            ["completed", "skipped"],
            ContractLiteralCodec.GetLiterals<DaemonCleanupStatus>());

        Assert.Equal(
            [
                nameof(DaemonCleanupSkipReason.Running),
                nameof(DaemonCleanupSkipReason.UnsafeInvalidSession),
                nameof(DaemonCleanupSkipReason.UncertainReachability),
            ],
            Enum.GetNames<DaemonCleanupSkipReason>());
        Assert.Equal(
            ["running", "unsafeInvalidSession", "uncertainReachability"],
            ContractLiteralCodec.GetLiterals<DaemonCleanupSkipReason>());
    }
}
