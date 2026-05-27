namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
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
    public async Task WaitForStartup_WhenTimeoutExceedsProbeAttemptCap_PassesProbeAttemptCapToSessionAwaiter ()
    {
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Success(CreateSession()),
        };
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            new StubUnityLogReader(),
            new StubDaemonProcessIdentityAssessor());

        var result = await observer.WaitForStartupAsync(
            CreateContext("fingerprint-gui-observer-session"),
            processId: 4321,
            processStartedAtUtc: DateTimeOffset.UtcNow,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonTimeouts.ProbeAttemptTimeoutCap, awaiter.LastTimeout);
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
        Assert.Equal(DaemonStartupBlockingReasonValues.Compile, result.Blocker!.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, result.Blocker.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Blocker.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, result.Blocker.ActionRequired);
        Assert.Equal(processStartedAtUtc, result.Blocker.ProcessStartedAtUtc);
        Assert.NotNull(result.Blocker.PrimaryDiagnostic);
        Assert.Equal(DaemonDiagnosisStartupPhaseValues.ScriptCompilation, result.Blocker.StartupPhase);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, result.Blocker.PrimaryDiagnostic!.Kind);
        Assert.Equal("CS1739", result.Blocker.PrimaryDiagnostic!.Code);
        Assert.Equal("Assets/Foo.cs", result.Blocker.PrimaryDiagnostic.File);
        Assert.Equal(74, result.Blocker.PrimaryDiagnostic.Line);
        Assert.Equal(17, result.Blocker.PrimaryDiagnostic.Column);
        Assert.Equal("Missing parameter", result.Blocker.PrimaryDiagnostic.Message);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(
        "An error occurred while resolving packages:\nProject has invalid dependencies:\ncom.example.missing: Package cannot be found\n",
        DaemonStartupBlockingReasonValues.PackageResolution,
        DaemonDiagnosisReasonValues.UnityPackageResolutionFailed,
        DaemonStartupRetryDispositionValues.RetryAfterFix,
        DaemonDiagnosisStartupPhaseValues.PackageResolution,
        DaemonDiagnosisActionRequiredValues.ResolvePackages,
        DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution)]
    [InlineData(
        "Unity Editor entered Safe Mode and is waiting for user action.\n",
        DaemonStartupBlockingReasonValues.SafeMode,
        DaemonDiagnosisReasonValues.EditorUserActionRequired,
        DaemonStartupRetryDispositionValues.ManualActionRequired,
        DaemonDiagnosisStartupPhaseValues.UserAction,
        DaemonDiagnosisActionRequiredValues.ResolveUnityDialog,
        DaemonDiagnosisPrimaryDiagnosticKindValues.UnityDialog)]
    [InlineData(
        "Could not load file or assembly 'MackySoft.Ucli.Infrastructure'\n",
        DaemonStartupBlockingReasonValues.UcliPlugin,
        DaemonDiagnosisReasonValues.UcliPluginDependencyMissing,
        DaemonStartupRetryDispositionValues.RetryAfterFix,
        DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
        DaemonDiagnosisActionRequiredValues.ResolvePackages,
        DaemonDiagnosisPrimaryDiagnosticKindValues.PluginDependency)]
    [InlineData(
        "Multiple precompiled assemblies with the same name Newtonsoft.Json.dll included on the current platform.\n",
        DaemonStartupBlockingReasonValues.PrecompiledAssemblyConflict,
        DaemonDiagnosisReasonValues.PrecompiledAssemblyConflict,
        DaemonStartupRetryDispositionValues.RetryAfterFix,
        DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
        DaemonDiagnosisActionRequiredValues.FixCompileErrors,
        DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler)]
    public async Task WaitForStartup_WhenClassifiedStartupBlockerAppearsInLog_ReturnsExpectedBlocker (
        string logText,
        string expectedStartupBlockingReason,
        string expectedReason,
        string expectedRetryDisposition,
        string expectedStartupPhase,
        string expectedActionRequired,
        string expectedPrimaryDiagnosticKind)
    {
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(logText, truncated: false, path: "/tmp/unity.log", sizeBytes: logText.Length),
        };
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, new StubDaemonProcessIdentityAssessor());

        var result = await observer.WaitForStartupAsync(
            CreateContext($"fingerprint-gui-observer-{expectedReason}"),
            processId: Environment.ProcessId,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(expectedStartupBlockingReason, result.Blocker!.StartupBlockingReason);
        Assert.Equal(expectedReason, result.Blocker!.Reason);
        Assert.Equal(expectedRetryDisposition, result.Blocker.RetryDisposition);
        Assert.Equal(expectedStartupPhase, result.Blocker.StartupPhase);
        Assert.Equal(expectedActionRequired, result.Blocker.ActionRequired);
        Assert.Equal(expectedPrimaryDiagnosticKind, result.Blocker.PrimaryDiagnostic!.Kind);
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
        Assert.Equal(DaemonStartupBlockingReasonValues.ProcessExit, result.Blocker!.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDispositionValues.Unknown, result.Blocker.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap, result.Blocker.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, result.Blocker.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit, result.Blocker.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenProcessExitsWithClassifiedLog_ReturnsLogBlocker ()
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
        var processIdentityAssessor = new StubDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.NotRunning,
                ObservedStartTimeUtc: null,
                Error: null),
        };
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, processIdentityAssessor);

        var result = await observer.WaitForStartupAsync(
            CreateContext("fingerprint-gui-observer-exit-with-log"),
            processId: int.MaxValue,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(DaemonStartupBlockingReasonValues.Compile, result.Blocker!.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, result.Blocker.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Blocker.Reason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, result.Blocker.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenTimeoutHasNoClassifiedLogAndProcessIsAlive_ReturnsEndpointRegistrationTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var awaiter = new StubDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
            OnWaitForSession = () => timeProvider.Advance(TimeSpan.FromMilliseconds(20)),
        };
        var logReader = new StubUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, truncated: false, path: "/tmp/unity.log", sizeBytes: 0),
        };
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            new StubDaemonProcessIdentityAssessor(),
            timeProvider);

        var result = await observer.WaitForStartupAsync(
            CreateContext("fingerprint-gui-observer-unclassified-timeout"),
            processId: Environment.ProcessId,
            processStartedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero),
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(10),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsBlocked);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error.Code);
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
            EditorMode: "gui",
            OwnerKind: "cli",
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

        public Action? OnWaitForSession { get; set; }

        public int CallCount { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public DateTimeOffset? LastExpectedProcessStartedAtUtc { get; private set; }

        public ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
            ResolvedUnityProjectContext unityProject,
            int expectedProcessId,
            TimeSpan timeout,
            DateTimeOffset? expectedProcessStartedAtUtc = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            OnWaitForSession?.Invoke();
            LastTimeout = timeout;
            LastExpectedProcessStartedAtUtc = expectedProcessStartedAtUtc;
            return ValueTask.FromResult(NextResult);
        }
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
