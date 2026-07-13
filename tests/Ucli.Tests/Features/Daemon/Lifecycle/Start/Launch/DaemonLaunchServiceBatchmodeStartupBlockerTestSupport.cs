using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Daemon.DaemonLaunchServiceTestSupport;

internal static class DaemonLaunchServiceBatchmodeStartupBlockerTestSupport
{
    public const string CompileBlockerMessage = "Unity scripts have compiler errors.";

    public static ClassifiedBlockerScenario CreateClassifiedBlockerScenario (
        string projectFingerprint,
        int processId,
        DaemonPrimaryDiagnostic? primaryDiagnostic = null)
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(projectFingerprint);
        var initialSession = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress);
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
        var updatedSession = DaemonSessionTestFactory.Create(
            processId: processId,
            sessionToken: LaunchSessionToken,
            projectFingerprint: context.ProjectFingerprint,
            endpointAddress: LaunchEndpointAddress,
            processStartedAtUtc: processStartedAtUtc);
        var classification = CreateCompileBlockerClassification(primaryDiagnostic);
        var launchSessionService = new RecordingDaemonLaunchSessionService
        {
            InitializeResult = DaemonLaunchSessionWriteResult.Success(initialSession),
            UpdateProcessIdResult = DaemonLaunchSessionWriteResult.Success(updatedSession),
        };
        var launcher = new RecordingUnityDaemonProcessLauncher
        {
            NextResult = UnityDaemonLaunchResult.Success(processId, processStartedAtUtc),
        };
        var readinessProbe = new RecordingDaemonStartupReadinessProbe
        {
            NextResult = DaemonStartupReadinessProbeResult.Failure(
                ExecutionError.InternalError(classification.Message, DaemonErrorCodes.DaemonStartupBlocked),
                classification),
        };
        var compensationService = new RecordingDaemonLaunchCompensationService();
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore();
        var service = CreateService(
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore,
            launchAttemptStore: launchAttemptStore);

        return new ClassifiedBlockerScenario(
            context,
            classification,
            processId,
            processStartedAtUtc,
            launchSessionService,
            launcher,
            readinessProbe,
            compensationService,
            diagnosisStore,
            launchAttemptStore,
            service);
    }

    public static DaemonStartupFailureClassification CreateCompileBlockerClassification (
        DaemonPrimaryDiagnostic? primaryDiagnostic = null)
    {
        return new DaemonStartupFailureClassification(
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
            Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix),
            Message: CompileBlockerMessage,
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation),
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: primaryDiagnostic);
    }

    internal sealed class ClassifiedBlockerScenario
    {
        public ClassifiedBlockerScenario (
            ResolvedUnityProjectContext context,
            DaemonStartupFailureClassification classification,
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
            Classification = classification;
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

        public DaemonStartupFailureClassification Classification { get; }

        public int ProcessId { get; }

        public DateTimeOffset ProcessStartedAtUtc { get; }

        public RecordingDaemonLaunchSessionService LaunchSessionService { get; }

        public RecordingUnityDaemonProcessLauncher Launcher { get; }

        public RecordingDaemonStartupReadinessProbe ReadinessProbe { get; }

        public RecordingDaemonLaunchCompensationService CompensationService { get; }

        public RecordingDaemonDiagnosisStore DiagnosisStore { get; }

        public RecordingDaemonLaunchAttemptStore LaunchAttemptStore { get; }

        public DaemonLaunchService Service { get; }

        public ValueTask<DaemonStartResult> LaunchAsync (
            DaemonStartupBlockedProcessPolicy onStartupBlocked = DaemonStartupBlockedProcessPolicy.Auto)
        {
            return Service.LaunchAsync(
                Context,
                TimeSpan.FromMilliseconds(500),
                DaemonEditorMode.Batchmode,
                onStartupBlocked,
                cancellationToken: CancellationToken.None);
        }
    }
}
