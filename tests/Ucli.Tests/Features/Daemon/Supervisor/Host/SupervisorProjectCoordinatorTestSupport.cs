using System.Diagnostics;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorProjectCoordinatorTestSupport
{
    public static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    public static SupervisorProjectCoordinator CreateCoordinator (
        IDaemonStartOperation startOperation,
        IDaemonStopOperation stopOperation,
        IDaemonPingClient pingClient,
        IDaemonDiagnosisStore diagnosisStore,
        IDaemonSessionStore sessionStore,
        IDaemonArtifactCleaner? artifactCleaner = null,
        TimeProvider? timeProvider = null)
    {
        var effectiveTimeProvider = timeProvider ?? new ManualTimeProvider();
        var runtimeLogger = new SupervisorRuntimeLogger();
        var stabilityVerifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore),
            new DaemonCompensationOperationOwner(),
            effectiveTimeProvider);
        var exitHandler = new SupervisorExitHandler(
            sessionStore,
            artifactCleaner ?? new RecordingDaemonArtifactCleaner(),
            new SupervisorDiagnosisWriter(diagnosisStore),
            runtimeLogger);
        return new SupervisorProjectCoordinator(
            startOperation,
            stopOperation,
            pingClient,
            new DaemonReachabilityClassifier(),
            stabilityVerifier,
            exitHandler,
            runtimeLogger,
            effectiveTimeProvider);
    }

    public static TestDirectoryScope CreateUnityProjectScope (string testCaseName)
    {
        return TestDirectories.CreateTempScope(
            "supervisor-project-coordinator",
            testCaseName,
            DirectoryCleanupMode.BestEffort);
    }

    public static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: ResolvedUnityProjectContextTestFactory.RepositoryRoot,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
    }

    public static ResolvedUnityProjectContext CreateUnityProject (TestDirectoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: scope.FullPath,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
    }

    public static DaemonSession CreateExitedProcessSession ()
    {
        return DaemonSessionTestFactory.Create(sessionToken: "session-token", processId: int.MaxValue);
    }

    internal sealed class SupervisorOwnedDaemonProcess : IDisposable
    {
        private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(5);

        private readonly Process process;

        private SupervisorOwnedDaemonProcess (Process process)
        {
            this.process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public static SupervisorOwnedDaemonProcess Start ()
        {
            return new SupervisorOwnedDaemonProcess(TestProcessInvocations.StartLongRunningProcess());
        }

        public DaemonSession CreateSession ()
        {
            return DaemonSessionTestFactory.Create(sessionToken: "session-token", processId: process.Id);
        }

        public void RequestTermination ()
        {
            TestProcessAwaiter.TerminateBestEffort(process);
        }

        public async Task TerminateAndAwaitCoordinatorAsync (SupervisorProjectCoordinator coordinator)
        {
            RequestTermination();
            await AwaitExitAndCoordinatorAsync(coordinator);
        }

        public async Task AwaitExitAndCoordinatorAsync (SupervisorProjectCoordinator coordinator)
        {
            ArgumentNullException.ThrowIfNull(coordinator);

            await TestProcessAwaiter.WaitForExitAsync(
                    process,
                    "Managed daemon helper process",
                    ProcessExitTimeout)
                .ConfigureAwait(false);
            await TestAwaiter.WaitAsync(
                    coordinator.AwaitManagedProcessesAsync(),
                    "Supervisor managed daemon cleanup",
                    SignalWaitTimeout)
                .ConfigureAwait(false);
        }

        public void Dispose ()
        {
            process.Dispose();
        }
    }
}
