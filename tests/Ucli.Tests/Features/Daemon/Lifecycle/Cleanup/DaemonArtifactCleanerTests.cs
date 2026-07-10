using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonArtifactCleanerTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Cleanup_WhenSuccessorAppearsBeforeOwnershipCheck_PreservesSuccessorArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "no-session-successor");
        const string projectFingerprint = "fingerprint-no-session-successor";
        var sessionStore = new DaemonSessionStore(
            new DaemonSessionJsonSerializer(),
            new DaemonSessionValidator());
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var cleaner = new DaemonArtifactCleaner(sessionStore, lifecycleStore, launchAttemptStore);
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: scope.FullPath,
            projectFingerprint: projectFingerprint);
        var successorSession = DaemonSessionTestFactory.Create(
            sessionToken: "successor-session-token",
            projectFingerprint: projectFingerprint);
        var lockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(scope.FullPath, projectFingerprint);
        using var publicationLock = await FileExclusiveLock.AcquireAsync(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        var cleanupTask = cleaner.CleanupIfSessionMissingAsync(unityProject, CancellationToken.None).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(cleanupTask.IsCompleted);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        await FileUtilities.WriteAllTextAtomicallyAsync(
            sessionPath,
            new DaemonSessionJsonSerializer().Serialize(successorSession) + Environment.NewLine,
            CancellationToken.None);
        publicationLock.Dispose();

        var cleanupResult = await cleanupTask;
        var currentSessionResult = await sessionStore.ReadAsync(
            scope.FullPath,
            projectFingerprint,
            CancellationToken.None);

        Assert.True(cleanupResult.IsSuccess);
        Assert.True(currentSessionResult.IsSuccess);
        Assert.Equal(successorSession, currentSessionResult.Session);
        Assert.Empty(lifecycleStore.DeleteInvocations);
        Assert.Empty(launchAttemptStore.PruneInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfSessionMatches_WhenSuccessorWriteStartsDuringCleanup_PublishesSuccessorAfterCleanup ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "concurrent-successor-write");
        const string projectFingerprint = "fingerprint-concurrent-successor";
        var retiredSession = DaemonSessionTestFactory.Create(
            sessionToken: "retired-session-token",
            projectFingerprint: projectFingerprint);
        var successorSession = retiredSession with
        {
            SessionToken = "successor-session-token",
            IssuedAtUtc = retiredSession.IssuedAtUtc.AddSeconds(1),
        };
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(
            sessionPath,
            new DaemonSessionJsonSerializer().Serialize(retiredSession) + Environment.NewLine,
            CancellationToken.None);
        var cleanupReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCleanupRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupSessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, _) =>
            {
                cleanupReadStarted.TrySetResult();
                await allowCleanupRead.Task;
                return DaemonSessionReadResult.Success(retiredSession);
            },
        };
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: scope.FullPath,
            projectFingerprint: projectFingerprint);
        var cleaner = new DaemonArtifactCleaner(
            cleanupSessionStore,
            new RecordingDaemonLifecycleStore(),
            new RecordingDaemonLaunchAttemptStore());
        var successorStore = new DaemonSessionStore(
            new DaemonSessionJsonSerializer(),
            new DaemonSessionValidator());

        var cleanupTask = cleaner.CleanupIfSessionMatchesAsync(
            unityProject,
            retiredSession,
            CancellationToken.None).AsTask();
        await cleanupReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var successorWriteTask = successorStore.WriteAsync(
            scope.FullPath,
            successorSession,
            CancellationToken.None).AsTask();
        Assert.False(successorWriteTask.IsCompleted);
        allowCleanupRead.TrySetResult();

        var cleanupResult = await cleanupTask;
        var successorWriteResult = await successorWriteTask;
        var currentSessionResult = await successorStore.ReadAsync(
            scope.FullPath,
            projectFingerprint,
            CancellationToken.None);

        Assert.True(cleanupResult.IsSuccess);
        Assert.True(successorWriteResult.IsSuccess);
        Assert.True(currentSessionResult.IsSuccess);
        Assert.Equal(successorSession, currentSessionResult.Session);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfSessionMatches_WhenObservedGenerationIsCurrent_DeletesSameGenerationArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "matching-session");
        const string projectFingerprint = "fingerprint-matching";
        var expectedSession = DaemonSessionTestFactory.Create(
            sessionToken: "matching-session-token",
            projectFingerprint: projectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(
            sessionPath,
            new DaemonSessionJsonSerializer().Serialize(expectedSession) + Environment.NewLine,
            CancellationToken.None);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(expectedSession),
        };
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(scope.FullPath, projectFingerprint);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, string.Empty, CancellationToken.None);
        }

        var cleaner = new DaemonArtifactCleaner(sessionStore, lifecycleStore, launchAttemptStore);
        var result = await cleaner.CleanupIfSessionMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: projectFingerprint),
            expectedSession,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(sessionPath));
        Assert.Single(lifecycleStore.DeleteInvocations);
        Assert.Single(launchAttemptStore.PruneInvocations);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.False(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfSessionMatches_WhenExpectedSessionIsAlreadyMissing_DeletesResidualArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "matching-session-missing");
        const string projectFingerprint = "fingerprint-matching-missing";
        var expectedSession = DaemonSessionTestFactory.Create(
            sessionToken: "missing-session-token",
            projectFingerprint: projectFingerprint);
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(scope.FullPath, projectFingerprint);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, string.Empty, CancellationToken.None);
        }

        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            lifecycleStore,
            launchAttemptStore);
        var result = await cleaner.CleanupIfSessionMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: projectFingerprint),
            expectedSession,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(lifecycleStore.DeleteInvocations);
        Assert.Single(launchAttemptStore.PruneInvocations);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.False(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfStoppedProcessMatches_WhenRotatedSessionBelongsToStoppedProcess_DeletesCurrentArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "stopped-process-rotation");
        const string ProjectFingerprint = "fingerprint-stopped-process-rotation";
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 10, 0, 0, 0, TimeSpan.Zero);
        var rotatedSession = DaemonSessionTestFactory.Create(
            sessionToken: "rotated-session-token",
            projectFingerprint: ProjectFingerprint,
            processId: 4123,
            processStartedAtUtc: processStartedAtUtc);
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(rotatedSession)),
            lifecycleStore,
            launchAttemptStore);

        var result = await cleaner.CleanupIfStoppedProcessMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprint),
            new DaemonProcessTerminationTarget(4123, processStartedAtUtc),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(lifecycleStore.DeleteInvocations);
        Assert.Single(launchAttemptStore.PruneInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfStoppedProcessMatches_WhenInvalidCurrentSessionBelongsToStoppedProcess_DeletesCurrentArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "stopped-process-invalid");
        const string ProjectFingerprint = "fingerprint-stopped-process-invalid";
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 10, 0, 0, 0, TimeSpan.Zero);
        var invalidSession = DaemonSessionTestFactory.Create(
            sessionToken: "invalid-session-token",
            projectFingerprint: ProjectFingerprint,
            processId: 4123,
            processStartedAtUtc: processStartedAtUtc);
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Failure(
                ExecutionError.InvalidArgument("invalid session"),
                DaemonSessionReadFailureKind.InvalidSession,
                invalidSession,
                DaemonSessionArtifactIdentity.Create("{ invalid session"))),
            lifecycleStore,
            launchAttemptStore);

        var result = await cleaner.CleanupIfStoppedProcessMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprint),
            new DaemonProcessTerminationTarget(4123, processStartedAtUtc),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(lifecycleStore.DeleteInvocations);
        Assert.Single(launchAttemptStore.PruneInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfStoppedProcessMatches_WhenCurrentSessionBelongsToAnotherProcess_PreservesCurrentArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "stopped-process-successor");
        const string ProjectFingerprint = "fingerprint-stopped-process-successor";
        var successorSession = DaemonSessionTestFactory.Create(
            sessionToken: "successor-session-token",
            projectFingerprint: ProjectFingerprint,
            processId: 9876,
            processStartedAtUtc: new DateTimeOffset(2026, 07, 10, 0, 1, 0, TimeSpan.Zero));
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(successorSession)),
            lifecycleStore,
            launchAttemptStore);

        var result = await cleaner.CleanupIfStoppedProcessMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprint),
            new DaemonProcessTerminationTarget(
                ProcessId: 4123,
                ProcessStartedAtUtc: new DateTimeOffset(2026, 07, 10, 0, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(lifecycleStore.DeleteInvocations);
        Assert.Empty(launchAttemptStore.PruneInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfStoppedProcessMatches_WhenSessionIsAlreadyMissing_DeletesResidualArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "stopped-process-missing");
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            lifecycleStore,
            launchAttemptStore);

        var result = await cleaner.CleanupIfStoppedProcessMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: "fingerprint-stopped-process-missing"),
            new DaemonProcessTerminationTarget(
                ProcessId: 4123,
                ProcessStartedAtUtc: new DateTimeOffset(2026, 07, 10, 0, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(lifecycleStore.DeleteInvocations);
        Assert.Single(launchAttemptStore.PruneInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfSessionArtifactMatches_WhenSuccessorReplacesInvalidObservation_PreservesSuccessorArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "invalid-successor-session");
        const string projectFingerprint = "fingerprint-invalid-successor";
        const string invalidSessionJson = "{ invalid session json";
        var successorSession = DaemonSessionTestFactory.Create(
            sessionToken: "successor-session-token",
            projectFingerprint: projectFingerprint);
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(sessionPath, invalidSessionJson, CancellationToken.None);
        var persistentSessionStore = new DaemonSessionStore(
            new DaemonSessionJsonSerializer(),
            new DaemonSessionValidator());
        var invalidObservation = await persistentSessionStore.ReadAsync(
            scope.FullPath,
            projectFingerprint,
            CancellationToken.None);
        var expectedArtifactIdentity = Assert.IsType<DaemonSessionArtifactIdentity>(invalidObservation.ArtifactIdentity);
        var successorWriteResult = await persistentSessionStore.WriteAsync(
            scope.FullPath,
            successorSession,
            CancellationToken.None);
        Assert.True(successorWriteResult.IsSuccess);
        var successorSessionJson = await File.ReadAllTextAsync(sessionPath, CancellationToken.None);
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(scope.FullPath, projectFingerprint);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, string.Empty, CancellationToken.None);
        }

        var cleaner = new DaemonArtifactCleaner(persistentSessionStore, lifecycleStore, launchAttemptStore);
        var result = await cleaner.CleanupIfSessionArtifactMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: projectFingerprint),
            expectedArtifactIdentity,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(successorSessionJson, await File.ReadAllTextAsync(sessionPath, CancellationToken.None));
        Assert.Empty(lifecycleStore.DeleteInvocations);
        Assert.Empty(launchAttemptStore.PruneInvocations);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfSessionArtifactMatches_WhenObservedInvalidArtifactIsCurrent_DeletesSameArtifactArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "invalid-current-session");
        const string projectFingerprint = "fingerprint-invalid-current";
        const string invalidSessionJson = "{ invalid session json";
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(sessionPath, invalidSessionJson, CancellationToken.None);
        var sessionStore = new DaemonSessionStore(
            new DaemonSessionJsonSerializer(),
            new DaemonSessionValidator());
        var invalidObservation = await sessionStore.ReadAsync(
            scope.FullPath,
            projectFingerprint,
            CancellationToken.None);
        var expectedArtifactIdentity = Assert.IsType<DaemonSessionArtifactIdentity>(invalidObservation.ArtifactIdentity);
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var cleaner = new DaemonArtifactCleaner(sessionStore, lifecycleStore, launchAttemptStore);

        var result = await cleaner.CleanupIfSessionArtifactMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: projectFingerprint),
            expectedArtifactIdentity,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(sessionPath));
        Assert.Single(lifecycleStore.DeleteInvocations);
        Assert.Single(launchAttemptStore.PruneInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfSessionArtifactMatches_WhenObservedArtifactIsAlreadyMissing_DeletesResidualArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "invalid-session-missing");
        const string ProjectFingerprint = "fingerprint-invalid-missing";
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(),
            lifecycleStore,
            launchAttemptStore);

        var result = await cleaner.CleanupIfSessionArtifactMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprint),
            DaemonSessionArtifactIdentity.Create("{ invalid session json"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(lifecycleStore.DeleteInvocations);
        Assert.Single(launchAttemptStore.PruneInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupIfSessionMatches_WhenSuccessorSessionIsCurrent_PreservesSuccessorArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "successor-session");
        const string projectFingerprint = "fingerprint-successor";
        var expectedSession = DaemonSessionTestFactory.Create(
            sessionToken: "retired-session-token",
            projectFingerprint: projectFingerprint);
        var successorSession = expectedSession with
        {
            SessionToken = "successor-session-token",
            IssuedAtUtc = expectedSession.IssuedAtUtc.AddSeconds(1),
        };
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(successorSession),
        };
        var lifecycleStore = new RecordingDaemonLifecycleStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(scope.FullPath, projectFingerprint);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, string.Empty, CancellationToken.None);
        }

        var cleaner = new DaemonArtifactCleaner(sessionStore, lifecycleStore, launchAttemptStore);
        var result = await cleaner.CleanupIfSessionMatchesAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: projectFingerprint),
            expectedSession,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(sessionStore.DeleteInvocations);
        Assert.Empty(lifecycleStore.DeleteInvocations);
        Assert.Empty(launchAttemptStore.PruneInvocations);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Cleanup_WhenUnixFallbackSocketDirectoryBecomesEmpty_DeletesFallbackDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "fallback-socket");
        var storageRoot = Path.Combine(scope.FullPath, new string('a', 160), new string('b', 160));
        const string projectFingerprint = "fingerprint-cleanup";
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);

        var socketPath = endpoint.Address;
        var socketDirectoryPath = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(socketDirectoryPath);
        await File.WriteAllTextAsync(socketPath, string.Empty, CancellationToken.None);

        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(),
            new RecordingDaemonLifecycleStore(),
            new RecordingDaemonLaunchAttemptStore());

        var result = await cleaner.CleanupIfSessionMissingAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: storageRoot,
                projectFingerprint: projectFingerprint),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(socketPath));
        Assert.False(Directory.Exists(socketDirectoryPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Cleanup_WhenLaunchAttemptStoreDeletesOldAttempts_ReturnsDeletedLaunchAttemptCount ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "prune-count");
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore
        {
            PruneResult = DaemonLaunchAttemptStoreOperationResult.Success(deletedCount: 3),
        };
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(),
            new RecordingDaemonLifecycleStore(),
            launchAttemptStore);

        var result = await cleaner.CleanupIfSessionMissingAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: "fingerprint-cleanup"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.DeletedLaunchAttemptCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Cleanup_WhenLaunchAttemptPruneFails_ReturnsFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "prune-failure");
        var pruneError = ExecutionError.InternalError("prune failed");
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(),
            new RecordingDaemonLifecycleStore(),
            new RecordingDaemonLaunchAttemptStore
            {
                PruneResult = DaemonLaunchAttemptStoreOperationResult.Failure(pruneError),
            });

        var result = await cleaner.CleanupIfSessionMissingAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: scope.FullPath,
                projectFingerprint: "fingerprint-cleanup"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(pruneError, result.Error);
    }

}
