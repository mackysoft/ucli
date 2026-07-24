using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;

namespace MackySoft.Ucli.Tests.Daemon;

using static DaemonLaunchAttemptStoreTestSupport;

public sealed class DaemonLaunchAttemptStoreWriteTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteFailure_WritesSchemaVersionOneAndGuidLaunchAttemptId ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "schema-version");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);

        var writeResult = await store.WriteFailureAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            attempt,
            CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(attempt.ArtifactPath.Value, CancellationToken.None));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(attempt.LaunchAttemptId, root.GetProperty("launchAttemptId").GetGuid());
    }
}
