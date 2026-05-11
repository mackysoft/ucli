using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiEditorAttachServiceTests
{
    private static readonly DateTimeOffset ProbeProcessStartedAtUtc = new(2026, 5, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenMatchingGuiSessionRegisters_ReturnsAlreadyRunning ()
    {
        var marker = CreateMarker();
        var context = CreateContext();
        var markerReader = new StubUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
        };
        var processProbe = new StubUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(ProbeProcessStartedAtUtc),
        };
        var session = CreateGuiSession();
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            Result = DaemonGuiSessionRegistrationWaitResult.Success(session),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = new DaemonGuiEditorAttachService(markerReader, processProbe, awaiter, diagnosisStore);

        var result = await service.TryAttachExistingGuiEditorAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(1, awaiter.CallCount);
        Assert.Equal(context, awaiter.LastUnityProject);
        Assert.Equal(marker.ProcessId, awaiter.LastExpectedProcessId);
        Assert.Equal(ProbeProcessStartedAtUtc, awaiter.LastExpectedProcessStartedAtUtc);
        Assert.True(awaiter.LastTimeout > TimeSpan.FromMilliseconds(450));
        Assert.True(awaiter.LastTimeout <= TimeSpan.FromMilliseconds(500));
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenBatchmodeRequestedAndGuiMarkerMatches_ReturnsEditorModeMismatch ()
    {
        var marker = CreateMarker();
        var markerReader = new StubUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
        };
        var processProbe = new StubUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(ProbeProcessStartedAtUtc),
        };
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = new DaemonGuiEditorAttachService(markerReader, processProbe, awaiter, diagnosisStore);

        var result = await service.TryAttachExistingGuiEditorAsync(
            CreateContext(),
            TimeSpan.FromMilliseconds(500),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonEditorModeMismatch, result.Error!.Code);
        Assert.Equal(0, awaiter.CallCount);
        Assert.Equal(0, diagnosisStore.WriteCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenEndpointRegistrationTimesOut_WritesGuiEndpointDiagnosis ()
    {
        var marker = CreateMarker();
        var markerReader = new StubUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
        };
        var processProbe = new StubUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(ProbeProcessStartedAtUtc),
        };
        var timeoutError = ExecutionError.Timeout("wait timed out", ExecutionErrorCodes.IpcTimeout);
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            Result = DaemonGuiSessionRegistrationWaitResult.Failure(timeoutError),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = new DaemonGuiEditorAttachService(markerReader, processProbe, awaiter, diagnosisStore);

        var result = await service.TryAttachExistingGuiEditorAsync(
            CreateContext(),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Terminate,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error.Code);
        Assert.Equal(1, awaiter.CallCount);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.GuiEndpointNotRegistered, diagnosisStore.LastDiagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, diagnosisStore.LastDiagnosis.ReportedBy);
        Assert.True(diagnosisStore.LastDiagnosis.IsInferred);
        Assert.Equal(marker.ProcessId, diagnosisStore.LastDiagnosis.ProcessId);
        Assert.Equal(marker.MarkerPath, diagnosisStore.LastDiagnosis.EditorInstancePath);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonEditorModeValues.Gui, result.Startup!.EditorMode);
        Assert.Equal(DaemonSessionOwnerKindValues.User, result.Startup.OwnerKind);
        Assert.False(result.Startup.CanShutdownProcess);
        Assert.Equal(marker.ProcessId, result.Startup.ProcessId);
        Assert.Equal(ProbeProcessStartedAtUtc, result.Startup.StartedAtUtc);
        Assert.Equal(DaemonStartupProcessActionValues.Kept, result.Startup.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenMarkerAndProbeConsumeTime_PassesRemainingTimeoutToAwaiter ()
    {
        var timeProvider = new ManualTimeProvider();
        var markerReader = new StubUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(CreateMarker()),
            OnRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(125)),
        };
        var processProbe = new StubUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(ProbeProcessStartedAtUtc),
            OnProbe = () => timeProvider.Advance(TimeSpan.FromMilliseconds(175)),
        };
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter();
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            new StubDaemonDiagnosisStore(),
            timeProvider);

        var result = await service.TryAttachExistingGuiEditorAsync(
            CreateContext(),
            TimeSpan.FromMilliseconds(1000),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(700), awaiter.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenProcessProbeRejectsMarker_ReturnsNullWithoutWaiting ()
    {
        var markerReader = new StubUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(CreateMarker()),
        };
        var processProbe = new StubUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotUnityEditor),
        };
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter();
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            new StubDaemonDiagnosisStore());

        var result = await service.TryAttachExistingGuiEditorAsync(
            CreateContext(),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Auto,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, awaiter.CallCount);
    }

    private static UnityEditorInstanceMarker CreateMarker ()
    {
        return new UnityEditorInstanceMarker(
            MarkerPath: "/repo/UnityProject/Library/EditorInstance.json",
            ProcessId: 1234,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 1, 0, TimeSpan.Zero),
            AppPath: "/Applications/Unity.app",
            AppContentsPath: "/Applications/Unity.app/Contents");
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateGuiSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 2, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorModeValues.Gui,
            OwnerKind: DaemonSessionOwnerKindValues.User,
            CanShutdownProcess: false,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: null);
    }

    private sealed class StubUnityEditorInstanceMarkerReader : IUnityEditorInstanceMarkerReader
    {
        public UnityEditorInstanceMarkerReadResult ReadResult { get; set; } =
            UnityEditorInstanceMarkerReadResult.Success(null);

        public Action? OnRead { get; set; }

        public ValueTask<UnityEditorInstanceMarkerReadResult> ReadAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            OnRead?.Invoke();
            return ValueTask.FromResult(ReadResult);
        }
    }

    private sealed class StubUnityGuiEditorProcessProbe : IUnityGuiEditorProcessProbe
    {
        public UnityGuiEditorProcessProbeResult Result { get; set; } =
            UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotRunning);

        public Action? OnProbe { get; set; }

        public ValueTask<UnityGuiEditorProcessProbeResult> ProbeAsync (
            UnityEditorInstanceMarker marker,
            CancellationToken cancellationToken = default)
        {
            OnProbe?.Invoke();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubDaemonGuiSessionRegistrationAwaiter : IDaemonGuiSessionRegistrationAwaiter
    {
        public DaemonGuiSessionRegistrationWaitResult Result { get; set; } =
            DaemonGuiSessionRegistrationWaitResult.Success(CreateGuiSession());

        public int CallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public int LastExpectedProcessId { get; private set; }

        public DateTimeOffset? LastExpectedProcessStartedAtUtc { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
            ResolvedUnityProjectContext unityProject,
            int expectedProcessId,
            TimeSpan timeout,
            DateTimeOffset? expectedProcessStartedAtUtc = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUnityProject = unityProject;
            LastExpectedProcessId = expectedProcessId;
            LastExpectedProcessStartedAtUtc = expectedProcessStartedAtUtc;
            LastTimeout = timeout;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public int WriteCallCount { get; private set; }

        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }
}
