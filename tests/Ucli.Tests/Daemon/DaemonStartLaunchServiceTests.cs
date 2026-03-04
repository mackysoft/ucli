namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonStartLaunchServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenLaunchAndReadinessSucceed_ReturnsStarted ()
    {
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(999),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Ready(),
        };
        var service = CreateService(
            daemonSessionStore: sessionStore,
            unityDaemonProcessLauncher: launcher,
            startupReadinessProbe: readinessProbe,
            processTerminationService: new StubDaemonProcessTerminationService(),
            artifactCleaner: new StubDaemonArtifactCleaner());

        var result = await service.Launch(CreateContext("fingerprint-launch-success"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, sessionStore.WriteCallCount);
        Assert.Equal(0, sessionStore.DeleteCallCount);
        Assert.Equal(1, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityLaunchFails_RunsCompensationAndReturnsLaunchFailure ()
    {
        var launchError = ExecutionError.InternalError("launch failed");
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Failure(launchError),
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = CreateService(
            daemonSessionStore: sessionStore,
            unityDaemonProcessLauncher: launcher,
            startupReadinessProbe: new StubDaemonStartupReadinessProbe(),
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await service.Launch(CreateContext("fingerprint-launch-fail"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(launchError, result.Error);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenSessionUpdateFails_RunsCompensationAndReturnsWriteFailure ()
    {
        var writeError = ExecutionError.InternalError("write failed");
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Failure(writeError));
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(2222),
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = CreateService(
            daemonSessionStore: sessionStore,
            unityDaemonProcessLauncher: launcher,
            startupReadinessProbe: new StubDaemonStartupReadinessProbe(),
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await service.Launch(CreateContext("fingerprint-session-update-fail"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(writeError, result.Error);
        Assert.Equal(2, sessionStore.WriteCallCount);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenReadinessProbeFails_RunsCompensationAndReturnsProbeFailure ()
    {
        var probeError = ExecutionError.Timeout("probe failed");
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        var launcher = new StubUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(7777),
        };
        var readinessProbe = new StubDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = CreateService(
            daemonSessionStore: sessionStore,
            unityDaemonProcessLauncher: launcher,
            startupReadinessProbe: readinessProbe,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);

        var result = await service.Launch(CreateContext("fingerprint-probe-fail"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(probeError, result.Error);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    private static DaemonStartLaunchService CreateService (
        IDaemonSessionStore daemonSessionStore,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner)
    {
        return new DaemonStartLaunchService(
            endpointResolver: new StubIpcEndpointResolver(),
            daemonSessionStore: daemonSessionStore,
            sessionTokenGenerator: new StubDaemonSessionTokenGenerator(),
            unityDaemonProcessLauncher: unityDaemonProcessLauncher,
            startupReadinessProbe: startupReadinessProbe,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private sealed class StubIpcEndpointResolver : IIpcEndpointResolver
    {
        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-endpoint");
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public Queue<DaemonSessionStoreOperationResult> WriteResults { get; } = new();

        public int WriteCallCount { get; private set; }

        public int DeleteCallCount { get; private set; }

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionReadResult.Success(null));
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            if (WriteResults.Count == 0)
            {
                return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
            }

            return ValueTask.FromResult(WriteResults.Dequeue());
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonSessionTokenGenerator : IDaemonSessionTokenGenerator
    {
        public string Create ()
        {
            return "new-session-token";
        }
    }

    private sealed class StubUnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
    {
        public UnityDaemonLaunchResult NextResult { get; set; } = UnityDaemonLaunchResult.Success(1000);

        public int CallCount { get; private set; }

        public ValueTask<UnityDaemonLaunchResult> Launch (
            ResolvedUnityProjectContext unityProject,
            string daemonLogPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
    {
        public DaemonStartupReadinessProbeResult NextResult { get; set; } = DaemonStartupReadinessProbeResult.Ready();

        public ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReady (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
            int? processId,
            DateTimeOffset? expectedIssuedAtUtc,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }
}