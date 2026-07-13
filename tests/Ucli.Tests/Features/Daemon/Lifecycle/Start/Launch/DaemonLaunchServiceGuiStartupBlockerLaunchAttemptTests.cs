using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchServiceGuiStartupBlockerLaunchAttemptTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Launch_WhenLaunchAttemptIdDirectoryAlreadyExists_RegeneratesLaunchAttemptId ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-service", "launch-attempt-id-collision");
        var context = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-id-collision"));
        Directory.CreateDirectory(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            context.RepositoryRoot,
            context.ProjectFingerprint,
            "20260312_000000Z_00000001"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var blocker = DaemonGuiStartupBlockerObservationTestFactory.Create(
            processId: 6543,
            processStartedAtUtc,
            unityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-id-collision/unity.log",
            startupBlockingReason: DaemonStartupBlockingReason.ProcessExit,
            reason: DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
            retryDisposition: DaemonStartupRetryDisposition.Unknown,
            message: "Unity Editor exited before bootstrap completed.",
            startupPhase: DaemonDiagnosisStartupPhase.ProcessExit,
            actionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog);
        var guiStartupObserver = new RecordingDaemonGuiStartupObserver
        {
            NextResult = DaemonGuiStartupObservationResult.Blocked(blocker),
        };
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            new RecordingDaemonLaunchSessionService(),
            new RecordingUnityDaemonProcessLauncher(),
            new RecordingDaemonStartupReadinessProbe(),
            new RecordingDaemonLaunchCompensationService(),
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Keep,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.NotNull(result.Startup);
        Assert.Equal("20260312_000000Z_00000002", result.Startup!.LaunchAttemptId);
        Assert.Equal("20260312_000000Z_00000002", DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context).LaunchAttemptId);
    }
}
