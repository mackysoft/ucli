using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonServiceTestContext
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
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    public static DaemonSessionOutput CreateSessionOutput ()
    {
        return new DaemonSessionOutput(
            ProjectFingerprint: "mapped-fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 1, 2, 3, TimeSpan.Zero),
            EditorMode: "mapped-editor-mode",
            OwnerKind: "mapped-owner",
            CanShutdownProcess: false,
            EndpointTransportKind: "mapped-transport",
            EndpointAddress: "mapped-endpoint",
            ProcessId: 4321,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
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
            EditorInstancePath: null,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero));
    }

    public static TestDirectoryScope CreateTempScope (string testCaseName)
    {
        return TestDirectories.CreateTempScope("daemon-command-service", testCaseName);
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

        public int? LastTimeoutMilliseconds { get; private set; }

        public UcliCommand LastTimeoutCommand { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonCommandExecutionContextResolutionResult> ResolveAsync (
            UcliCommand timeoutCommand,
            string? projectPath,
            int? timeoutMilliseconds,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTimeoutCommand = timeoutCommand;
            LastProjectPath = projectPath;
            LastTimeoutMilliseconds = timeoutMilliseconds;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class StubDaemonStatusOperation : IDaemonStatusOperation
    {
        public DaemonStatusResult StatusResult { get; set; } = DaemonStatusResult.NotRunning();

        public Action? OnGetStatus { get; set; }

        public int GetStatusCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStatusResult> GetStatusAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            GetStatusCallCount++;
            OnGetStatus?.Invoke();
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(StatusResult);
        }
    }

    internal sealed class StubSupervisorProjectGateway : IDaemonProjectLifecycleGateway
    {
        public DaemonStartResult EnsureRunningResult { get; set; } = DaemonStartResult.Started(CreateSession());

        public DaemonStopResult? TryStopProjectResult { get; set; }

        public Func<ResolvedUnityProjectContext, TimeSpan, DaemonEditorMode?, CancellationToken, ValueTask<DaemonStartResult>>? EnsureRunningHandler { get; set; }

        public Func<ResolvedUnityProjectContext, TimeSpan, CancellationToken, ValueTask<DaemonStopResult?>>? TryStopProjectHandler { get; set; }

        public int EnsureRunningCallCount { get; private set; }

        public int TryStopProjectCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastEnsureRunningUnityProject { get; private set; }

        public TimeSpan LastEnsureRunningTimeout { get; private set; }

        public DaemonEditorMode? LastEnsureRunningEditorMode { get; private set; }

        public CancellationToken LastEnsureRunningCancellationToken { get; private set; }

        public ResolvedUnityProjectContext? LastTryStopProjectUnityProject { get; private set; }

        public TimeSpan LastTryStopProjectTimeout { get; private set; }

        public CancellationToken LastTryStopProjectCancellationToken { get; private set; }

        public ValueTask<DaemonStartResult> EnsureRunningAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            DaemonEditorMode? editorMode,
            CancellationToken cancellationToken = default)
        {
            EnsureRunningCallCount++;
            LastEnsureRunningUnityProject = unityProject;
            LastEnsureRunningTimeout = timeout;
            LastEnsureRunningEditorMode = editorMode;
            LastEnsureRunningCancellationToken = cancellationToken;

            if (EnsureRunningHandler != null)
            {
                return EnsureRunningHandler(unityProject, timeout, editorMode, cancellationToken);
            }

            return ValueTask.FromResult(EnsureRunningResult);
        }

        public ValueTask<DaemonStopResult?> TryStopProjectAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            TryStopProjectCallCount++;
            LastTryStopProjectUnityProject = unityProject;
            LastTryStopProjectTimeout = timeout;
            LastTryStopProjectCancellationToken = cancellationToken;

            if (TryStopProjectHandler != null)
            {
                return TryStopProjectHandler(unityProject, timeout, cancellationToken);
            }

            return ValueTask.FromResult(TryStopProjectResult);
        }
    }

    internal sealed class StubDaemonStopOperation : IDaemonStopOperation
    {
        public DaemonStopResult StopResult { get; set; } = DaemonStopResult.Stopped();

        public int StopCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStopResult> StopAsync (
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

    internal sealed class StubDaemonPingInfoClient : IDaemonPingInfoClient
    {
        public IpcPingResponse Response { get; set; } = new(
            ServerVersion: "0.0.1",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: IpcEditorLifecycleStateCodec.Ready,
            BlockingReason: null,
            CompileGeneration: "1",
            DomainReloadGeneration: "1",
            CanAcceptExecutionRequests: true);

        public Exception? Exception { get; set; }

        public Action? OnPingAndRead { get; set; }

        public int CallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public string? LastSessionToken { get; private set; }

        public bool LastValidateProjectFingerprint { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<IpcPingResponse> PingAndReadAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            bool validateProjectFingerprint = true,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            OnPingAndRead?.Invoke();
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastSessionToken = sessionToken;
            LastValidateProjectFingerprint = validateProjectFingerprint;
            LastCancellationToken = cancellationToken;
            if (Exception != null)
            {
                return ValueTask.FromException<IpcPingResponse>(Exception);
            }

            return ValueTask.FromResult(Response);
        }
    }

    internal sealed class StubDaemonReachabilityClassifier : IDaemonReachabilityClassifier
    {
        private readonly Func<Exception, bool> isNotRunning;

        public StubDaemonReachabilityClassifier (Func<Exception, bool> isNotRunning)
        {
            this.isNotRunning = isNotRunning;
        }

        public bool IsNotRunning (Exception exception)
        {
            return isNotRunning(exception);
        }
    }

    internal sealed class StubDaemonLifecycleStore : IDaemonLifecycleStore
    {
        public DaemonLifecycleObservationReadResult ReadResult { get; set; } = DaemonLifecycleObservationReadResult.Success(null);

        public DaemonLifecycleStoreOperationResult DeleteResult { get; set; } = DaemonLifecycleStoreOperationResult.Success();

        public int ReadCallCount { get; private set; }

        public int DeleteCallCount { get; private set; }

        public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            return ValueTask.FromResult(DeleteResult);
        }
    }

    internal sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        public DaemonProcessIdentityAssessment Assessment { get; set; } = new(
            DaemonProcessIdentityAssessmentStatus.NotRunning,
            ObservedStartTimeUtc: null,
            Error: null);

        public int CallCount { get; private set; }

        public int LastProcessId { get; private set; }

        public DateTimeOffset? LastExpectedProcessStartedAtUtc { get; private set; }

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset? expectedProcessStartedAtUtc)
        {
            CallCount++;
            LastProcessId = processId;
            LastExpectedProcessStartedAtUtc = expectedProcessStartedAtUtc;
            return Assessment;
        }
    }

    internal sealed class StubDaemonSessionDiagnosisResolver : IDaemonSessionDiagnosisResolver
    {
        public DaemonDiagnosis? Diagnosis { get; set; }

        public Func<ResolvedUnityProjectContext, DaemonSession, DaemonDiagnosis?, CancellationToken, ValueTask<DaemonDiagnosis?>>? Handler { get; set; }

        public int CallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public DaemonSession? LastSession { get; private set; }

        public DaemonDiagnosis? LastPersistedDiagnosis { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonDiagnosis?> ResolveForSessionAsync (
            ResolvedUnityProjectContext unityProject,
            DaemonSession session,
            DaemonDiagnosis? persistedDiagnosis,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUnityProject = unityProject;
            LastSession = session;
            LastPersistedDiagnosis = persistedDiagnosis;
            LastCancellationToken = cancellationToken;
            if (Handler != null)
            {
                return Handler(unityProject, session, persistedDiagnosis, cancellationToken);
            }

            return ValueTask.FromResult(Diagnosis);
        }
    }

    internal sealed class StubDaemonCleanupOperation : IDaemonCleanupOperation
    {
        public DaemonCleanupResult CleanupResult { get; set; } = DaemonCleanupResult.Completed();

        public int CleanupCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonCleanupResult> CleanupAsync (
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
            ProcessId: 4321,
            EditorInstancePath: null);

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
}
