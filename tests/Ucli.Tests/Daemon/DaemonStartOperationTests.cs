namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonStartOperationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenStaleSessionDetected_StopsPreviousProcessBeforeCleanupAndLaunch ()
    {
        var previousSession = CreateSession(processId: 4242);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(previousSession),
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("timeout"))),
            unityDaemonProcessLauncher: new StubUnityDaemonProcessLauncher(UnityDaemonLaunchResult.Success(999)),
            startupReadinessProbe: new StubDaemonStartupReadinessProbe(DaemonStartupReadinessProbeResult.Ready()),
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);
        var context = CreateContext("fingerprint-start-stale-stop");

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(4242, processTerminationService.LastProcessId);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenStaleSessionDetectedAndProcessStopFails_ReturnsFailureWithoutLaunching ()
    {
        var processStopError = ExecutionError.InternalError("stop failed");
        var previousSession = CreateSession(processId: 8080);
        var sessionStore = new StubDaemonSessionStore
        {
            ReadResult = DaemonSessionReadResult.Success(previousSession),
        };
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(processStopError),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var launcher = new StubUnityDaemonProcessLauncher(UnityDaemonLaunchResult.Success(999));
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: new StubDaemonPingClient(() => ValueTask.FromException(new TimeoutException("timeout"))),
            unityDaemonProcessLauncher: launcher,
            startupReadinessProbe: new StubDaemonStartupReadinessProbe(DaemonStartupReadinessProbeResult.Ready()),
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner);
        var context = CreateContext("fingerprint-start-stale-stop-fail");

        var result = await operation.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(processStopError, result.Error);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(0, artifactCleaner.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    private static DaemonStartOperation CreateOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingClient daemonPingClient,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner)
    {
        return new DaemonStartOperation(
            lifecycleLockProvider: new StubDaemonLifecycleLockProvider(),
            daemonSessionStore: daemonSessionStore,
            endpointResolver: new IpcEndpointResolver(),
            daemonPingClient: daemonPingClient,
            unityDaemonProcessLauncher: unityDaemonProcessLauncher,
            startupReadinessProbe: startupReadinessProbe,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner,
            sessionTokenGenerator: new StubDaemonSessionTokenGenerator(),
            reachabilityClassifier: new DaemonReachabilityClassifier());
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (int? processId)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-test-endpoint",
            ProcessId: processId);
    }

    private sealed class StubDaemonLifecycleLockProvider : IDaemonLifecycleLockProvider
    {
        public ValueTask<IAsyncDisposable> Acquire (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IAsyncDisposable>(new NoopAsyncDisposable());
        }

        private sealed class NoopAsyncDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync ()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public DaemonSessionReadResult ReadResult { get; set; } = DaemonSessionReadResult.Success(null);

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<ValueTask> handler;

        public StubDaemonPingClient (Func<ValueTask> handler)
        {
            this.handler = handler;
        }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return handler();
        }
    }

    private sealed class StubUnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
    {
        private readonly UnityDaemonLaunchResult nextResult;

        public StubUnityDaemonProcessLauncher (UnityDaemonLaunchResult nextResult)
        {
            this.nextResult = nextResult;
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityDaemonLaunchResult> Launch (
            ResolvedUnityProjectContext unityProject,
            string daemonLogPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(nextResult);
        }
    }

    private sealed class StubDaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
    {
        private readonly DaemonStartupReadinessProbeResult nextResult;

        public StubDaemonStartupReadinessProbe (DaemonStartupReadinessProbeResult nextResult)
        {
            this.nextResult = nextResult;
        }

        public ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReady (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(nextResult);
        }
    }

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public int? LastProcessId { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
            int? processId,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProcessId = processId;
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

    private sealed class StubDaemonSessionTokenGenerator : IDaemonSessionTokenGenerator
    {
        public string Create ()
        {
            return "new-session-token";
        }
    }
}
