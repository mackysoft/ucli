namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

internal static class DaemonLaunchServiceBatchmodeReadinessFailureTestSupport
{
    public static readonly DateTimeOffset DefaultProcessStartedAtUtc =
        new(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);

    public static ReadinessFailureScenario CreateScenario (
        string projectFingerprint,
        ExecutionError probeError,
        int processId = 7777,
        DateTimeOffset? processStartedAtUtc = null,
        RecordingDaemonLaunchCompensationService? compensationService = null,
        RecordingDaemonDiagnosisStore? diagnosisStore = null,
        RecordingDaemonLaunchAttemptStore? launchAttemptStore = null)
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(projectFingerprint);
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var startedAtUtc = processStartedAtUtc ?? DefaultProcessStartedAtUtc;
        var updatedSession = DaemonSessionTestFactory.Create(
            processId: processId,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: startedAtUtc);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(processId, startedAtUtc),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(probeError),
        };
        var resolvedCompensationService = compensationService ?? new RecordingDaemonLaunchCompensationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var resolvedDiagnosisStore = diagnosisStore ?? new RecordingDaemonDiagnosisStore();
        var resolvedLaunchAttemptStore = launchAttemptStore ?? new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            resolvedCompensationService,
            resolvedDiagnosisStore,
            launchAttemptStore: resolvedLaunchAttemptStore);

        return new ReadinessFailureScenario(
            context,
            initialSession,
            updatedSession,
            processId,
            startedAtUtc,
            launchSessionService,
            launcher,
            readinessProbe,
            resolvedCompensationService,
            resolvedDiagnosisStore,
            resolvedLaunchAttemptStore,
            service);
    }

    internal sealed class ReadinessFailureScenario
    {
        public ReadinessFailureScenario (
            ResolvedUnityProjectContext context,
            DaemonSession initialSession,
            DaemonSession updatedSession,
            int processId,
            DateTimeOffset processStartedAtUtc,
            RecordingDaemonLaunchSessionService launchSessionService,
            RecordingUnityDaemonProcessLauncher launcher,
            RecordingDaemonStartupReadinessProbe readinessProbe,
            RecordingDaemonLaunchCompensationService compensationService,
            RecordingDaemonDiagnosisStore diagnosisStore,
            RecordingDaemonLaunchAttemptStore launchAttemptStore,
            DaemonLaunchService service)
        {
            Context = context;
            InitialSession = initialSession;
            UpdatedSession = updatedSession;
            ProcessId = processId;
            ProcessStartedAtUtc = processStartedAtUtc;
            LaunchSessionService = launchSessionService;
            Launcher = launcher;
            ReadinessProbe = readinessProbe;
            CompensationService = compensationService;
            DiagnosisStore = diagnosisStore;
            LaunchAttemptStore = launchAttemptStore;
            Service = service;
        }

        public ResolvedUnityProjectContext Context { get; }

        public DaemonSession InitialSession { get; }

        public DaemonSession UpdatedSession { get; }

        public int ProcessId { get; }

        public DateTimeOffset ProcessStartedAtUtc { get; }

        public RecordingDaemonLaunchSessionService LaunchSessionService { get; }

        public RecordingUnityDaemonProcessLauncher Launcher { get; }

        public RecordingDaemonStartupReadinessProbe ReadinessProbe { get; }

        public RecordingDaemonLaunchCompensationService CompensationService { get; }

        public RecordingDaemonDiagnosisStore DiagnosisStore { get; }

        public RecordingDaemonLaunchAttemptStore LaunchAttemptStore { get; }

        public DaemonLaunchService Service { get; }

        public ValueTask<DaemonStartResult> LaunchAsync (CancellationToken cancellationToken = default)
        {
            return Service.LaunchAsync(
                Context,
                TimeSpan.FromMilliseconds(500),
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: cancellationToken);
        }
    }
}
