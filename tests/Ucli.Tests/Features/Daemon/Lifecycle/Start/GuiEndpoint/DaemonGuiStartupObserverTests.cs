namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiEndpoint;

public sealed class DaemonGuiStartupObserverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenSessionRegistrationSucceeds_ReturnsSessionWithoutReadingLog ()
    {
        var session = CreateSession();
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Success(session),
        };
        var logReader = new StubUnityLogReader();
        var processIdentityAssessor = new StubDaemonProcessIdentityAssessor();
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, processIdentityAssessor);
        var processStartedAtUtc = DateTimeOffset.UtcNow;

        var result = await observer.WaitForStartupAsync(
            CreateContext("fingerprint-gui-observer-session"),
            processId: 4321,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        Assert.Equal(0, logReader.CallCount);
        Assert.Equal(processStartedAtUtc, awaiter.LastExpectedProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenCompilerErrorAppearsInLog_ReturnsBlockedCompilerDiagnosis ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Assets/Foo.cs(74,17): error CS1739: Missing parameter\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 64),
        };
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, new StubDaemonProcessIdentityAssessor());

        var result = await observer.WaitForStartupAsync(
            CreateContext("fingerprint-gui-observer-compiler"),
            processId: Environment.ProcessId,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Blocker!.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, result.Blocker.ActionRequired);
        Assert.Equal(processStartedAtUtc, result.Blocker.ProcessStartedAtUtc);
        Assert.NotNull(result.Blocker.PrimaryDiagnostic);
        Assert.Equal("CS1739", result.Blocker.PrimaryDiagnostic!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenProcessExitsWithoutClassifiedLog_ReturnsProcessExitBlocker ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, truncated: false, path: "/tmp/unity.log", sizeBytes: 0),
        };
        var processIdentityAssessor = new StubDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.NotRunning,
                ObservedStartTimeUtc: null,
                Error: null),
        };
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, processIdentityAssessor);

        var result = await observer.WaitForStartupAsync(
            CreateContext("fingerprint-gui-observer-exit"),
            processId: int.MaxValue,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap, result.Blocker!.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, result.Blocker.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit, result.Blocker.PrimaryDiagnostic!.Kind);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint-gui-observer-session",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: DaemonEditorModeValues.Gui,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ipc.sock",
            ProcessId: 4321,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private sealed class StubDaemonGuiSessionRegistrationAwaiter : IDaemonGuiSessionRegistrationAwaiter
    {
        public DaemonGuiSessionRegistrationWaitResult NextResult { get; set; } =
            DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout"));

        public ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
            ResolvedUnityProjectContext unityProject,
            int expectedProcessId,
            TimeSpan timeout,
            DateTimeOffset? expectedProcessStartedAtUtc = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastExpectedProcessStartedAtUtc = expectedProcessStartedAtUtc;
            return ValueTask.FromResult(NextResult);
        }

        public DateTimeOffset? LastExpectedProcessStartedAtUtc { get; private set; }
    }

    private sealed class StubUnityLogReader : IUnityLogReader
    {
        public UnityLogReadResult NextResult { get; set; } = UnityLogReadResult.Success(string.Empty, false, "/tmp/unity.log", 0);

        public int CallCount { get; private set; }

        public ValueTask<UnityLogReadResult> ReadTailAsync (
            string storageRoot,
            string projectFingerprint,
            int maxBytes = IUnityLogReader.DefaultMaxBytes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        public DaemonProcessIdentityAssessment Assessment { get; set; } =
            new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                ObservedStartTimeUtc: DateTimeOffset.UtcNow,
                Error: null);

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset? expectedProcessStartedAtUtc)
        {
            return Assessment;
        }
    }
}
