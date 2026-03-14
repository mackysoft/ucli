using MackySoft.Tests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Supervisor;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

internal static class DaemonCommandServiceTestContext
{
    public static DaemonCommandExecutionContext CreateExecutionContext (
        int timeoutMilliseconds,
        string repositoryRoot = "/tmp/repo-root")
    {
        return new DaemonCommandExecutionContext(
            Context: new ProjectContext(
                UnityProject: new ResolvedUnityProjectContext(
                    UnityProjectRoot: "/tmp/unity-project",
                    RepositoryRoot: repositoryRoot,
                    ProjectFingerprint: "fingerprint",
                    PathSource: UnityProjectPathSource.CommandOption),
                Config: UcliConfig.CreateDefault(),
                ConfigSource: ConfigSource.Default),
            Timeout: TimeSpan.FromMilliseconds(timeoutMilliseconds));
    }

    public static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "secret-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            OwnerProcessId: 9876);
    }

    public static DaemonSessionOutput CreateSessionOutput ()
    {
        return new DaemonSessionOutput(
            ProjectFingerprint: "mapped-fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 1, 2, 3, TimeSpan.Zero),
            RuntimeKind: "mapped-runtime",
            OwnerKind: "mapped-owner",
            CanShutdownProcess: false,
            EndpointTransportKind: "mapped-transport",
            EndpointAddress: "mapped-endpoint",
            ProcessId: 4321,
            OwnerProcessId: 8765);
    }

    public static DaemonDiagnosis CreateDiagnosis ()
    {
        return new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.ShutdownRequested,
            Message: "daemon shutdown completed",
            ReportedBy: DaemonDiagnosisReportedByValues.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 05, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 1234,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero));
    }

    public static TestDirectoryScope CreateTempScope (string testCaseName)
    {
        return TestDirectories.CreateTempScope("daemon-command-service", testCaseName);
    }

    public static SupervisorInstanceManifest CreateSupervisorManifest (
        string storageRoot,
        string sessionToken = "supervisor-session-token")
    {
        var endpoint = new SupervisorEndpointResolver().Resolve(storageRoot);
        return new SupervisorInstanceManifest(
            ProcessId: 2468,
            SessionToken: sessionToken,
            EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
            EndpointAddress: endpoint.Address,
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 2, 3, 4, TimeSpan.Zero));
    }

    public static async Task WriteSupervisorManifest (
        string storageRoot,
        SupervisorInstanceManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var store = new SupervisorManifestStore();
        await store.Write(storageRoot, manifest, cancellationToken).ConfigureAwait(false);
    }

    public static SupervisorClient CreateSupervisorClient (IIpcTransportClient transportClient)
    {
        return new SupervisorClient(transportClient);
    }

    public static SupervisorBootstrapper CreateSupervisorBootstrapper (
        IIpcTransportClient transportClient,
        ISupervisorProcessLauncher? processLauncher = null)
    {
        var externalProcessRunner = new SupervisorExternalProcessRunner();
        processLauncher ??= new SupervisorProcessLauncher(
            new SupervisorLaunchCommandResolver(),
            new LaunchdSupervisorProcessLauncher(externalProcessRunner),
            new SystemdRunSupervisorProcessLauncher(externalProcessRunner),
            new WindowsDetachedSupervisorProcessLauncher());
        var supervisorClient = CreateSupervisorClient(transportClient);
        return new SupervisorBootstrapper(
            new SupervisorManifestStore(),
            supervisorClient,
            processLauncher,
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver());
    }

    public static IpcResponse CreateSuccessResponse<TPayload> (
        IpcRequest request,
        TPayload payload)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateErrorResponse (
        IpcRequest request,
        string code,
        string message)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new IpcError(code, message, null),
            ]);
    }

    internal sealed class StubDaemonCommandExecutionContextResolver : IDaemonCommandExecutionContextResolver
    {
        private readonly DaemonCommandExecutionContextResolutionResult result;

        public StubDaemonCommandExecutionContextResolver (DaemonCommandExecutionContextResolutionResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public string? LastProjectPath { get; private set; }

        public string? LastTimeoutOption { get; private set; }

        public UcliCommand LastTimeoutCommand { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonCommandExecutionContextResolutionResult> Resolve (
            UcliCommand timeoutCommand,
            string? projectPath,
            string? timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTimeoutCommand = timeoutCommand;
            LastProjectPath = projectPath;
            LastTimeoutOption = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class StubDaemonStartOperation : IDaemonStartOperation
    {
        public DaemonStartResult StartResult { get; set; } = DaemonStartResult.Started(CreateSession());

        public int StartCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStartResult> Start (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(StartResult);
        }
    }

    internal sealed class StubDaemonStopOperation : IDaemonStopOperation
    {
        public DaemonStopResult StopResult { get; set; } = DaemonStopResult.Stopped();

        public int StopCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStopResult> Stop (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(StopResult);
        }
    }

    internal sealed class StubDaemonStatusOperation : IDaemonStatusOperation
    {
        public DaemonStatusResult StatusResult { get; set; } = DaemonStatusResult.NotRunning();

        public int GetStatusCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStatusResult> GetStatus (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            GetStatusCallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(StatusResult);
        }
    }

    internal sealed class StubDaemonCleanupOperation : IDaemonCleanupOperation
    {
        public DaemonCleanupResult CleanupResult { get; set; } = DaemonCleanupResult.Completed();

        public int CleanupCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonCleanupResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CleanupCallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(CleanupResult);
        }
    }

    internal sealed class StubDaemonSessionOutputMapper : IDaemonSessionOutputMapper
    {
        public DaemonSessionOutput Output { get; set; } = CreateSessionOutput();

        public DaemonSession? LastSession { get; private set; }

        public int CallCount { get; private set; }

        public DaemonSessionOutput ToOutput (DaemonSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            LastSession = session;
            CallCount++;
            return Output;
        }
    }

    internal sealed class StubDaemonDiagnosisOutputMapper : IDaemonDiagnosisOutputMapper
    {
        public DaemonDiagnosisOutput Output { get; set; } = new(
            Reason: DaemonDiagnosisReasonValues.ShutdownRequested,
            Message: "mapped diagnosis",
            ReportedBy: DaemonDiagnosisReportedByValues.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 05, 4, 5, 6, TimeSpan.Zero),
            ProcessId: 4321);

        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public int CallCount { get; private set; }

        public DaemonDiagnosisOutput ToOutput (DaemonDiagnosis diagnosis)
        {
            ArgumentNullException.ThrowIfNull(diagnosis);

            LastDiagnosis = diagnosis;
            CallCount++;
            return Output;
        }
    }

    internal sealed record StubIpcTransportCall (
        IpcEndpoint Endpoint,
        IpcRequest Request,
        TimeSpan Timeout);

    internal sealed class StubIpcTransportClient : IIpcTransportClient
    {
        public Func<IpcEndpoint, IpcRequest, TimeSpan, CancellationToken, ValueTask<IpcResponse>>? SendHandler { get; set; }

        public List<StubIpcTransportCall> Calls { get; } = [];

        public ValueTask<IpcResponse> SendAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new StubIpcTransportCall(endpoint, request, timeout));
            if (SendHandler == null)
            {
                throw new InvalidOperationException("Stub IPC transport handler is not configured.");
            }

            return SendHandler(endpoint, request, timeout, cancellationToken);
        }
    }
}