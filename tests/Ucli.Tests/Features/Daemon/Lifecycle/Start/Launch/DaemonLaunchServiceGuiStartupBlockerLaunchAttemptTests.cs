using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchServiceGuiStartupBlockerLaunchAttemptTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Launch_WhenStartupIsBlocked_ProjectsGeneratedLaunchAttemptId ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-service", "launch-attempt-id");
        var context = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-id"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(processStartedAtUtc);
        var guiLauncher = new RecordingUnityGuiEditorProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(6543, processStartedAtUtc),
        };
        var blocker = DaemonGuiStartupBlockerObservationTestFactory.Create(
            processId: 6543,
            processStartedAtUtc,
            unityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint-id/unity.log",
            startupBlockingReason: DaemonStartupBlockingReason.ProcessExit,
            reason: DaemonDiagnosisReason.EditorExitedBeforeBootstrap,
            retryDisposition: DaemonStartupRetryDisposition.Unknown,
            message: "Unity Editor exited before bootstrap completed.",
            startupPhase: DaemonDiagnosisStartupPhase.ProcessExit,
            actionRequired: DaemonDiagnosisActionRequired.InspectUnityLog);
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
            timeProvider,
            new RecordingDaemonDiagnosisStore(),
            unityGuiEditorProcessLauncher: guiLauncher,
            guiStartupObserver: guiStartupObserver,
            launchAttemptStore: launchAttemptStore);

        var result = await service.LaunchAsync(
            context,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Keep,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.NotNull(result.Startup);
        var expectedLaunchAttemptId = new Guid(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(expectedLaunchAttemptId, result.Startup!.LaunchAttemptId);
        Assert.Equal(expectedLaunchAttemptId, DaemonLaunchAttemptStoreAssert.LatestLaunchAttemptWrittenFor(launchAttemptStore, context).LaunchAttemptId);
    }
}
