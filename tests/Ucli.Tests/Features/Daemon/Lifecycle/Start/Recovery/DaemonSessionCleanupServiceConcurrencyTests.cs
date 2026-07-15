using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionCleanupServiceConcurrencyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupInvalidSessionArtifacts_WhenSuccessorPublishesAfterObservation_PreservesSuccessorWithoutTermination ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-session-cleanup-service",
            "invalid-successor-publication");
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-invalid-successor-publication");
        var processStartedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var invalidContract = new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionGenerationId: Guid.Empty,
            SessionToken: "tampered-invalid-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: processStartedAtUtc,
            EditorMode: DaemonEditorMode.Batchmode,
            OwnerKind: DaemonSessionOwnerKind.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKind.NamedPipe,
            EndpointAddress: "tampered-endpoint",
            ProcessId: 3131,
            ProcessStartedAtUtc: processStartedAtUtc,
            OwnerProcessId: 4141,
            EditorInstanceId: null);
        await File.WriteAllTextAsync(
            sessionPath,
            DaemonSessionJsonContractSerializer.Serialize(invalidContract) + Environment.NewLine,
            CancellationToken.None);
        var sessionStore = new DaemonSessionStore();
        var invalidObservation = await sessionStore.ReadAsync(
            scope.FullPath,
            projectFingerprint,
            CancellationToken.None);
        Assert.False(invalidObservation.IsSuccess);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, invalidObservation.FailureKind);
        Assert.NotNull(invalidObservation.ArtifactIdentity);

        var successorSession = DaemonSessionTestFactory.Create(
            sessionToken: "successor-session-token",
            projectFingerprint: projectFingerprint,
            processId: 5151,
            processStartedAtUtc: processStartedAtUtc.AddMinutes(1));
        var successorWriteResult = await sessionStore.WriteAsync(
            scope.FullPath,
            successorSession,
            CancellationToken.None);
        Assert.True(successorWriteResult.IsSuccess);
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var processTerminationService = new RecordingProcessTerminationService();
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            new DaemonArtifactCleaner(sessionStore, lifecycleStore, launchAttemptStore),
            new DaemonInvalidSessionCleanupSafetyEvaluator(
                new RecordingDaemonProcessIdentityAssessor(
                    DaemonProcessIdentityAssessmentStatus.DifferentProcess)),
            new DaemonCompensationOperationOwner());
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: scope.FullPath,
            projectFingerprint: projectFingerprint);

        var cleanupResult = await service.CleanupInvalidSessionArtifactsAsync(
            unityProject,
            invalidObservation,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), new ManualTimeProvider()),
            CancellationToken.None);
        var currentSessionResult = await sessionStore.ReadAsync(
            scope.FullPath,
            projectFingerprint,
            CancellationToken.None);

        Assert.True(cleanupResult.IsSuccess);
        Assert.False(processTerminationService.WasCalled);
        Assert.True(currentSessionResult.IsSuccess);
        Assert.Equal(successorSession, Assert.IsType<DaemonSession>(currentSessionResult.Session));
        Assert.Empty(lifecycleStore.DeleteInvocations);
        Assert.Empty(launchAttemptStore.PruneInvocations);
    }

    private sealed class RecordingProcessTerminationService : IDaemonProcessTerminationService
    {
        public bool WasCalled { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
            DaemonProcessTerminationTarget? target,
            ExecutionDeadline deadline,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }
}
