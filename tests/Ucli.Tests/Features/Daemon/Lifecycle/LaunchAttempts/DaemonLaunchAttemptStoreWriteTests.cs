using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;

namespace MackySoft.Ucli.Tests.Daemon;

using static DaemonLaunchAttemptStoreTestSupport;

public sealed class DaemonLaunchAttemptStoreWriteTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteFailure_WhenLaunchAttemptIdIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "invalid-id");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, DaemonStartupStatus.Failed) with
        {
            LaunchAttemptId = " ",
        };

        var writeResult = await store.WriteFailureAsync(scope.FullPath, ProjectFingerprint, attempt, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }
}
