namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonManagementServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionIsReachable_ReturnsAlreadyRunningWithoutLaunchingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-management", "start-already-running");
        var context = CreateContext(scope.FullPath, "fingerprint-start-already-running");
        var sessionStore = new DaemonSessionStore();
        var existingSession = CreateSession(context.ProjectFingerprint, "existing-token");
        await sessionStore.Write(scope.FullPath, existingSession, CancellationToken.None);

        var launcher = new StubUnityDaemonProcessLauncher((_, _) => UnityDaemonLaunchResult.Success(777));
        var service = CreateService(
            sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            unityIpcClient: new StubUnityIpcClient(),
            unityDaemonProcessLauncher: launcher,
            daemonLogReader: new StubDaemonLogReader());

        var result = await service.Start(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionIsStale_DeletesOldSessionAndStartsNewDaemon ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-management", "start-stale-session");
        var context = CreateContext(scope.FullPath, "fingerprint-start-stale");
        var sessionStore = new DaemonSessionStore();
        var staleSession = CreateSession(context.ProjectFingerprint, "stale-token");
        await sessionStore.Write(scope.FullPath, staleSession, CancellationToken.None);

        var pingClient = new StubDaemonPingClient(
            () => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused)),
            static () => ValueTask.CompletedTask);
        var launcher = new StubUnityDaemonProcessLauncher((_, _) => UnityDaemonLaunchResult.Success(9876));
        var service = CreateService(
            sessionStore,
            daemonPingClient: pingClient,
            unityIpcClient: new StubUnityIpcClient(),
            unityDaemonProcessLauncher: launcher,
            daemonLogReader: new StubDaemonLogReader());

        var result = await service.Start(context, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, launcher.CallCount);

        var loadedSessionResult = await sessionStore.Read(scope.FullPath, context.ProjectFingerprint, CancellationToken.None);
        Assert.True(loadedSessionResult.IsSuccess);
        Assert.True(loadedSessionResult.Exists);
        var loadedSession = Assert.IsType<DaemonSession>(loadedSessionResult.Session);
        Assert.NotEqual("stale-token", loadedSession.SessionToken);
        Assert.Equal(9876, loadedSession.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSessionDoesNotExist_ReturnsNotRunning ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-management", "stop-not-running");
        var context = CreateContext(scope.FullPath, "fingerprint-stop-not-running");
        var service = CreateService(
            new DaemonSessionStore(),
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            unityIpcClient: new StubUnityIpcClient(),
            unityDaemonProcessLauncher: new StubUnityDaemonProcessLauncher((_, _) => UnityDaemonLaunchResult.Success(1)),
            daemonLogReader: new StubDaemonLogReader());

        var result = await service.Stop(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.NotRunning, result.Status);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenShutdownReturnsSessionTokenInvalid_TreatsAsStoppedAndCleansSession ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-management", "stop-token-invalid");
        var context = CreateContext(scope.FullPath, "fingerprint-stop-token-invalid");
        var sessionStore = new DaemonSessionStore();
        var session = CreateSession(context.ProjectFingerprint, "session-token") with
        {
            ProcessId = null,
        };
        await sessionStore.Write(scope.FullPath, session, CancellationToken.None);

        var unityIpcClient = new StubUnityIpcClient(request => new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusError,
            Payload: JsonSerializer.SerializeToElement(new { }),
            Errors: new[]
            {
                new IpcError(IpcErrorCodes.SessionTokenInvalid, "invalid token", null),
            }));
        var service = CreateService(
            sessionStore,
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            unityIpcClient: unityIpcClient,
            unityDaemonProcessLauncher: new StubUnityDaemonProcessLauncher((_, _) => UnityDaemonLaunchResult.Success(1)),
            daemonLogReader: new StubDaemonLogReader());

        var result = await service.Stop(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.True(result.IsSuccess);

        var readResult = await sessionStore.Read(scope.FullPath, context.ProjectFingerprint, CancellationToken.None);
        Assert.True(readResult.IsSuccess);
        Assert.False(readResult.Exists);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenSessionExistsAndPingIsTokenInvalid_ReturnsStale ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-management", "status-stale-token-invalid");
        var context = CreateContext(scope.FullPath, "fingerprint-status-stale");
        var sessionStore = new DaemonSessionStore();
        var session = CreateSession(context.ProjectFingerprint, "session-token");
        await sessionStore.Write(scope.FullPath, session, CancellationToken.None);

        var pingClient = new StubDaemonPingClient(() => ValueTask.FromException(new DaemonPingResponseException(
            "token invalid",
            IpcErrorCodes.SessionTokenInvalid)));
        var service = CreateService(
            sessionStore,
            daemonPingClient: pingClient,
            unityIpcClient: new StubUnityIpcClient(),
            unityDaemonProcessLauncher: new StubUnityDaemonProcessLauncher((_, _) => UnityDaemonLaunchResult.Success(1)),
            daemonLogReader: new StubDaemonLogReader());

        var result = await service.GetStatus(context, TimeSpan.FromMilliseconds(300), CancellationToken.None);

        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadLogs_DelegatesToLogReaderWithRequestedMaxBytes ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-management", "read-logs");
        var context = CreateContext(scope.FullPath, "fingerprint-read-logs");
        var logReader = new StubDaemonLogReader
        {
            NextResult = DaemonLogReadResult.Success("log-tail", true, "/tmp/daemon.log", 999),
        };

        var service = CreateService(
            new DaemonSessionStore(),
            daemonPingClient: new StubDaemonPingClient(static () => ValueTask.CompletedTask),
            unityIpcClient: new StubUnityIpcClient(),
            unityDaemonProcessLauncher: new StubUnityDaemonProcessLauncher((_, _) => UnityDaemonLaunchResult.Success(1)),
            daemonLogReader: logReader);

        var result = await service.ReadLogs(context, maxBytes: 128, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("log-tail", result.Text);
        Assert.True(result.Truncated);
        Assert.Equal(context.RepositoryRoot, logReader.LastStorageRoot);
        Assert.Equal(context.ProjectFingerprint, logReader.LastProjectFingerprint);
        Assert.Equal(128, logReader.LastMaxBytes);
    }

    private static DaemonManagementService CreateService (
        IDaemonSessionStore sessionStore,
        IDaemonPingClient daemonPingClient,
        IUnityIpcClient unityIpcClient,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonLogReader daemonLogReader)
    {
        var endpointResolver = new IpcEndpointResolver();
        var reachabilityClassifier = new DaemonReachabilityClassifier();
        var startOperation = new DaemonStartOperation(
            lifecycleLockProvider: new InMemoryDaemonLifecycleLockProvider(),
            daemonSessionStore: sessionStore,
            endpointResolver: endpointResolver,
            daemonPingClient: daemonPingClient,
            unityDaemonProcessLauncher: unityDaemonProcessLauncher,
            startupReadinessProbe: new DaemonStartupReadinessProbe(daemonPingClient),
            processTerminationService: new DaemonProcessTerminationService(),
            artifactCleaner: new DaemonArtifactCleaner(sessionStore, endpointResolver),
            sessionTokenGenerator: new DaemonSessionTokenGenerator(),
            reachabilityClassifier: reachabilityClassifier);
        var stopOperation = new DaemonStopOperation(
            lifecycleLockProvider: new InMemoryDaemonLifecycleLockProvider(),
            daemonSessionStore: sessionStore,
            shutdownClient: new DaemonShutdownClient(unityIpcClient),
            processTerminationService: new DaemonProcessTerminationService(),
            artifactCleaner: new DaemonArtifactCleaner(sessionStore, endpointResolver));
        var statusOperation = new DaemonStatusOperation(
            daemonSessionStore: sessionStore,
            daemonPingClient: daemonPingClient,
            reachabilityClassifier: reachabilityClassifier);
        return new DaemonManagementService(
            startOperation: startOperation,
            stopOperation: stopOperation,
            statusOperation: statusOperation,
            daemonLogReader: daemonLogReader);
    }

    private static ResolvedUnityProjectContext CreateContext (
        string repositoryRoot,
        string projectFingerprint)
    {
        var unityProjectRoot = Path.Combine(repositoryRoot, "UnityProject");
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        string projectFingerprint,
        string sessionToken)
    {
        var issuedAtUtc = DateTimeOffset.UtcNow;
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionToken,
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: issuedAtUtc,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-test",
            ProcessId: 4444);
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Queue<Func<ValueTask>> handlers;

        public StubDaemonPingClient (params Func<ValueTask>[] handlers)
        {
            this.handlers = new Queue<Func<ValueTask>>(handlers);
        }

        public int CallCount { get; private set; }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (handlers.Count == 0)
            {
                return ValueTask.CompletedTask;
            }

            return handlers.Dequeue().Invoke();
        }
    }

    private sealed class StubUnityIpcClient : IUnityIpcClient
    {
        private readonly Func<IpcRequest, IpcResponse> responseFactory;

        public StubUnityIpcClient (Func<IpcRequest, IpcResponse>? responseFactory = null)
        {
            this.responseFactory = responseFactory ?? (request => new IpcResponse(
                ProtocolVersion: request.ProtocolVersion,
                RequestId: request.RequestId,
                Status: IpcProtocol.StatusOk,
                Payload: JsonSerializer.SerializeToElement(new { }),
                Errors: Array.Empty<IpcError>()));
        }

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(responseFactory(request));
        }
    }

    private sealed class StubUnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
    {
        private readonly Func<ResolvedUnityProjectContext, string, UnityDaemonLaunchResult> launchHandler;

        public StubUnityDaemonProcessLauncher (Func<ResolvedUnityProjectContext, string, UnityDaemonLaunchResult> launchHandler)
        {
            this.launchHandler = launchHandler;
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityDaemonLaunchResult> Launch (
            ResolvedUnityProjectContext unityProject,
            string daemonLogPath,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(launchHandler(unityProject, daemonLogPath));
        }
    }

    private sealed class StubDaemonLogReader : IDaemonLogReader
    {
        public DaemonLogReadResult NextResult { get; set; } = DaemonLogReadResult.Success(string.Empty, false, string.Empty, 0);

        public string? LastStorageRoot { get; private set; }

        public string? LastProjectFingerprint { get; private set; }

        public int LastMaxBytes { get; private set; }

        public ValueTask<DaemonLogReadResult> ReadTail (
            string storageRoot,
            string projectFingerprint,
            int maxBytes = DaemonLogReader.DefaultMaxBytes,
            CancellationToken cancellationToken = default)
        {
            LastStorageRoot = storageRoot;
            LastProjectFingerprint = projectFingerprint;
            LastMaxBytes = maxBytes;
            return ValueTask.FromResult(NextResult);
        }
    }
}
