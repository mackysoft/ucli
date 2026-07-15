using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Git;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonListQueryServiceTestSupport
{
    public static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    public static DaemonListQueryService CreateSingleWorktreeService (
        ResolvedUnityProjectContext currentProject,
        DaemonSessionReadResult sessionReadResult,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonPingInfoClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        TimeProvider? timeProvider = null,
        IDaemonLifecycleStore? daemonLifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null,
        IDaemonSessionDiagnosisResolver? daemonSessionDiagnosisResolver = null)
    {
        return CreateService(
            new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: currentProject.UnityProjectRoot == currentProject.RepositoryRoot ? "." : "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            RecordingUnityProjectResolver.FromContexts(currentProject),
            new RecordingDaemonSessionStore(sessionReadResult),
            daemonDiagnosisStore,
            daemonPingClient,
            reachabilityClassifier,
            timeProvider,
            daemonLifecycleStore,
            processIdentityAssessor,
            daemonSessionDiagnosisResolver);
    }

    public static DaemonListQueryService CreateService (
        IGitWorktreeQueryService gitWorktreeQueryService,
        IUnityProjectResolver unityProjectResolver,
        IDaemonSessionStore daemonSessionStore,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonPingInfoClient daemonPingClient,
        IDaemonReachabilityClassifier daemonReachabilityClassifier,
        TimeProvider? timeProvider = null,
        IDaemonLifecycleStore? daemonLifecycleStore = null,
        IDaemonProcessIdentityAssessor? processIdentityAssessor = null,
        IDaemonSessionDiagnosisResolver? daemonSessionDiagnosisResolver = null)
    {
        return new DaemonListQueryService(
            gitWorktreeQueryService,
            unityProjectResolver,
            daemonSessionStore,
            daemonDiagnosisStore,
            new DaemonSessionProbe(
                daemonSessionStore,
                daemonPingClient,
                daemonReachabilityClassifier),
            daemonReachabilityClassifier,
            daemonLifecycleStore ?? new RecordingDaemonLifecycleStore(),
            processIdentityAssessor ?? new RecordingDaemonProcessIdentityAssessor(),
            daemonSessionDiagnosisResolver ?? CreateDiagnosisResolver(daemonDiagnosisStore),
            new DaemonDiagnosisOutputMapper(),
            new DefaultWorktreeProjectPathResolver(),
            timeProvider ?? TimeProvider.System);
    }

    public static ResolvedUnityProjectContext CreateUnityProject (
        string worktreeRoot,
        string projectRelativePath,
        string fingerprintLabel)
    {
        var normalizedWorktreeRoot = Path.GetFullPath(worktreeRoot);
        var normalizedProjectRoot = projectRelativePath == "."
            ? normalizedWorktreeRoot
            : Path.Combine(normalizedWorktreeRoot, projectRelativePath);
        return ProjectContextTestFactory.CreateUnityProject(
            unityProjectRoot: normalizedProjectRoot,
            repositoryRoot: normalizedWorktreeRoot,
            projectFingerprint: ProjectFingerprintTestFactory.Create(fingerprintLabel),
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
    }

    public static DaemonDiagnosis CreateDiagnosis (
        DaemonSession session,
        DaemonDiagnosisReason reason)
    {
        return new DaemonDiagnosis(
            Reason: reason,
            Message: $"diagnosis:{reason}",
            ReportedBy: DaemonDiagnosisReportedBy.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 12, 1, 0, TimeSpan.Zero),
            ProcessId: session.ProcessId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: session.IssuedAtUtc,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
            UnityLogPath: "/repo/.ucli/local/fingerprints/fp-current/unity.log",
            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequired.InspectUnityLog,
            PrimaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.ProcessExit,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: "process exited"));
    }

    public static RecordingDaemonPingInfoClient CreateDefaultPingClient ()
    {
        return CreatePingClient(static (unityProject, _, _, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(CreatePingResponse(unityProject));
        });
    }

    public static RecordingDaemonPingInfoClient CreateThrowingPingClient (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return CreatePingClient((_, _, _, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromException<IpcUnityEditorObservation>(exception);
        });
    }

    public static RecordingDaemonPingInfoClient CreatePingClient (
        Func<ResolvedUnityProjectContext, TimeSpan, string?, bool, CancellationToken, ValueTask<IpcUnityEditorObservation>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return new RecordingDaemonPingInfoClient
        {
            PingAndReadHandler = handler,
        };
    }

    public static IpcUnityEditorObservation CreatePingResponse (ResolvedUnityProjectContext unityProject)
    {
        return IpcUnityEditorObservationTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint);
    }

    private static RecordingDaemonSessionDiagnosisResolver CreateDiagnosisResolver (IDaemonDiagnosisStore daemonDiagnosisStore)
    {
        return new RecordingDaemonSessionDiagnosisResolver
        {
            Handler = async (unityProject, session, persistedDiagnosis, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (persistedDiagnosis is not null
                    && persistedDiagnosis.SessionIssuedAtUtc == session.IssuedAtUtc)
                {
                    return persistedDiagnosis;
                }

                if (session.ProcessId is not int processId)
                {
                    return null;
                }

                var diagnosis = new DaemonDiagnosis(
                    Reason: DaemonDiagnosisReason.ExternalTerminationSuspected,
                    Message: "Daemon process is no longer alive and no persisted diagnosis matched the current session.",
                    ReportedBy: DaemonDiagnosisReportedBy.Cli,
                    IsInferred: true,
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    ProcessId: processId,
                    EditorInstancePath: null,
                    SessionIssuedAtUtc: session.IssuedAtUtc,
                    ProcessStartedAtUtc: null,
                    UnityLogPath: null,
                    StartupPhase: null,
                    ActionRequired: null,
                    PrimaryDiagnostic: null);

                await daemonDiagnosisStore.WriteAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        diagnosis,
                        cancellationToken)
                    .ConfigureAwait(false);
                return diagnosis;
            },
        };
    }
}
