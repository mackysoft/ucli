using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;

public sealed class DaemonLaunchSessionServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Initialize_WhenSessionWriteSucceeds_ReturnsPersistedSession ()
    {
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        var service = new DaemonLaunchSessionService(
            endpointResolver: new StubIpcEndpointResolver(),
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StubDaemonSessionTokenGenerator());
        var context = CreateContext("fingerprint-session-init");

        var result = await service.Initialize(context, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var session = Assert.IsType<DaemonSession>(result.Session);
        Assert.Equal(context.ProjectFingerprint, session.ProjectFingerprint);
        Assert.Equal(1, sessionStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Initialize_WhenSessionWriteFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("initial write failed");
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Failure(expectedError));
        var service = new DaemonLaunchSessionService(
            endpointResolver: new StubIpcEndpointResolver(),
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StubDaemonSessionTokenGenerator());

        var result = await service.Initialize(CreateContext("fingerprint-session-init-fail"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, sessionStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateProcessId_WhenProcessIdIsNull_ReturnsOriginalSessionWithoutWrite ()
    {
        var sessionStore = new StubDaemonSessionStore();
        var service = new DaemonLaunchSessionService(
            endpointResolver: new StubIpcEndpointResolver(),
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StubDaemonSessionTokenGenerator());
        var session = CreateSession(processId: null);

        var result = await service.UpdateProcessId(
            CreateContext("fingerprint-session-no-update"),
            session,
            processId: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        Assert.Equal(0, sessionStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateProcessId_WhenProcessIdWriteFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("update write failed");
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Failure(expectedError));
        var service = new DaemonLaunchSessionService(
            endpointResolver: new StubIpcEndpointResolver(),
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StubDaemonSessionTokenGenerator());
        var session = CreateSession(processId: null);

        var result = await service.UpdateProcessId(
            CreateContext("fingerprint-session-update-fail"),
            session,
            processId: 4321,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, sessionStore.WriteCallCount);
        Assert.Equal(4321, sessionStore.LastWrittenSession?.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateProcessId_WhenProcessIdWriteSucceeds_ReturnsUpdatedSession ()
    {
        var sessionStore = new StubDaemonSessionStore();
        sessionStore.WriteResults.Enqueue(DaemonSessionStoreOperationResult.Success());
        var service = new DaemonLaunchSessionService(
            endpointResolver: new StubIpcEndpointResolver(),
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StubDaemonSessionTokenGenerator());
        var session = CreateSession(processId: null);

        var result = await service.UpdateProcessId(
            CreateContext("fingerprint-session-update-success"),
            session,
            processId: 8765,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(8765, result.Session?.ProcessId);
        Assert.Equal(1, sessionStore.WriteCallCount);
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
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,

            OwnerProcessId: 9876);
    }

    private sealed class StubIpcEndpointResolver : IIpcEndpointResolver
    {
        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-daemon-endpoint");
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public Queue<DaemonSessionStoreOperationResult> WriteResults { get; } = new();

        public int WriteCallCount { get; private set; }

        public DaemonSession? LastWrittenSession { get; private set; }

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
            LastWrittenSession = session;
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
}
