namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Process;

public sealed class DaemonGuiStartupObserverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenSessionRegistrationSucceeds_ReturnsSessionWithoutReadingLog ()
    {
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: "fingerprint-gui-observer-session",
            editorMode: DaemonEditorMode.Gui,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ipc.sock",
            processId: 4321);
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Success(
                session,
                IpcUnityEditorObservationTestFactory.Create(
                    editorMode: DaemonEditorMode.Gui,
                    projectFingerprint: session.ProjectFingerprint)),
        };
        var logReader = new UnexpectedUnityLogReader(
            "Session registration success should not read the Unity log.");
        var processIdentityAssessor = RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess();
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, processIdentityAssessor);
        var processStartedAtUtc = DateTimeOffset.UtcNow;

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-session"),
            processId: 4321,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        DaemonGuiSessionRegistrationAwaiterAssert.RegistrationWaitRequestedFor(
            awaiter,
            expectedProcessId: 4321,
            expectedProcessStartedAtUtc: processStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenTimeoutExceedsProbeAttemptCap_PassesProbeAttemptCapToSessionAwaiter ()
    {
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = CreateSuccessfulWaitResult(DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                projectFingerprint: "fingerprint-gui-observer-session",
                editorMode: DaemonEditorMode.Gui,
                endpointTransportKind: IpcTransportKind.UnixDomainSocket,
                endpointAddress: "/tmp/ipc.sock",
                processId: 4321)),
        };
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            new UnexpectedUnityLogReader("Session registration success should not read the Unity log."),
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess());

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-session"),
            processId: 4321,
            processStartedAtUtc: DateTimeOffset.UtcNow,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonGuiSessionRegistrationAwaiterAssert.RegistrationWaitRequestedWithTimeout(
            awaiter,
            DaemonTimeouts.ProbeAttemptTimeoutCap);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenCompilerErrorAppearsInLog_ReturnsBlockedCompilerDiagnosis ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Assets/Foo.cs(74,17): error CS1739: Missing parameter\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 64),
        };
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess());

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-compiler"),
            processId: Environment.ProcessId,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        var blockerObservation = Assert.IsType<DaemonGuiStartupBlockerObservation>(result.BlockerObservation);
        var classification = blockerObservation.Classification;
        Assert.Equal(DaemonStartupBlockingReason.Compile, classification.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.RetryAfterFix, classification.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, classification.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, classification.ActionRequired);
        Assert.Equal(processStartedAtUtc, blockerObservation.ProcessStartedAtUtc);
        var primaryDiagnostic = Assert.IsType<DaemonPrimaryDiagnostic>(classification.PrimaryDiagnostic);
        Assert.Equal(DaemonDiagnosisStartupPhase.ScriptCompilation, classification.StartupPhase);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, primaryDiagnostic.Kind);
        Assert.Equal("CS1739", primaryDiagnostic.Code);
        Assert.Equal("Assets/Foo.cs", primaryDiagnostic.File);
        Assert.Equal(74, primaryDiagnostic.Line);
        Assert.Equal(17, primaryDiagnostic.Column);
        Assert.Equal("Missing parameter", primaryDiagnostic.Message);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(
        "An error occurred while resolving packages:\nProject has invalid dependencies:\ncom.example.missing: Package cannot be found\n",
        DaemonStartupBlockingReason.PackageResolution,
        DaemonDiagnosisReasonValues.UnityPackageResolutionFailed,
        DaemonStartupRetryDisposition.RetryAfterFix,
        DaemonDiagnosisStartupPhase.PackageResolution,
        DaemonDiagnosisActionRequiredValues.ResolvePackages,
        DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution)]
    [InlineData(
        "Unity Editor entered Safe Mode and is waiting for user action.\n",
        DaemonStartupBlockingReason.SafeMode,
        DaemonDiagnosisReasonValues.EditorUserActionRequired,
        DaemonStartupRetryDisposition.ManualActionRequired,
        DaemonDiagnosisStartupPhase.UserAction,
        DaemonDiagnosisActionRequiredValues.ResolveUnityDialog,
        DaemonDiagnosisPrimaryDiagnosticKindValues.UnityDialog)]
    [InlineData(
        "Could not load file or assembly 'MackySoft.Ucli.Infrastructure'\n",
        DaemonStartupBlockingReason.UcliPlugin,
        DaemonDiagnosisReasonValues.UcliPluginDependencyMissing,
        DaemonStartupRetryDisposition.RetryAfterFix,
        DaemonDiagnosisStartupPhase.ScriptCompilation,
        DaemonDiagnosisActionRequiredValues.ResolvePackages,
        DaemonDiagnosisPrimaryDiagnosticKindValues.PluginDependency)]
    [InlineData(
        "Multiple precompiled assemblies with the same name Newtonsoft.Json.dll included on the current platform.\n",
        DaemonStartupBlockingReason.PrecompiledAssemblyConflict,
        DaemonDiagnosisReasonValues.PrecompiledAssemblyConflict,
        DaemonStartupRetryDisposition.RetryAfterFix,
        DaemonDiagnosisStartupPhase.ScriptCompilation,
        DaemonDiagnosisActionRequiredValues.FixCompileErrors,
        DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler)]
    public async Task WaitForStartup_WhenClassifiedStartupBlockerAppearsInLog_ReturnsExpectedBlocker (
        string logText,
        DaemonStartupBlockingReason expectedStartupBlockingReason,
        string expectedReason,
        DaemonStartupRetryDisposition expectedRetryDisposition,
        DaemonDiagnosisStartupPhase expectedStartupPhase,
        string expectedActionRequired,
        string expectedPrimaryDiagnosticKind)
    {
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(logText, truncated: false, path: "/tmp/unity.log", sizeBytes: logText.Length),
        };
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess());

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext($"fingerprint-gui-observer-{expectedReason}"),
            processId: Environment.ProcessId,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        var blockerObservation = Assert.IsType<DaemonGuiStartupBlockerObservation>(result.BlockerObservation);
        var classification = blockerObservation.Classification;
        Assert.Equal(expectedStartupBlockingReason, classification.StartupBlockingReason);
        Assert.Equal(expectedReason, classification.Reason);
        Assert.Equal(expectedRetryDisposition, classification.RetryDisposition);
        Assert.Equal(expectedStartupPhase, classification.StartupPhase);
        Assert.Equal(expectedActionRequired, classification.ActionRequired);
        Assert.Equal(expectedPrimaryDiagnosticKind, classification.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenProcessExitsWithoutClassifiedLog_ReturnsProcessExitBlocker ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, truncated: false, path: "/tmp/unity.log", sizeBytes: 0),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.NotRunning,
                ObservedStartTimeUtc: null,
                Error: null),
        };
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, processIdentityAssessor);

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-exit"),
            processId: int.MaxValue,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        var blockerObservation = Assert.IsType<DaemonGuiStartupBlockerObservation>(result.BlockerObservation);
        var classification = blockerObservation.Classification;
        Assert.Equal(DaemonStartupBlockingReason.ProcessExit, classification.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.Unknown, classification.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap, classification.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, classification.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit, classification.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenProcessExitsWithClassifiedLog_ReturnsLogBlocker ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero);
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Assets/Foo.cs(74,17): error CS1739: Missing parameter\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 64),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.NotRunning,
                ObservedStartTimeUtc: null,
                Error: null),
        };
        var observer = new DaemonGuiStartupObserver(awaiter, logReader, processIdentityAssessor);

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-exit-with-log"),
            processId: int.MaxValue,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        var blockerObservation = Assert.IsType<DaemonGuiStartupBlockerObservation>(result.BlockerObservation);
        var classification = blockerObservation.Classification;
        Assert.Equal(DaemonStartupBlockingReason.Compile, classification.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.RetryAfterFix, classification.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, classification.Reason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, classification.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenTimeoutHasNoClassifiedLogAndProcessIsAlive_ReturnsEndpointRegistrationTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
            OnWaitForSession = () => timeProvider.Advance(TimeSpan.FromMilliseconds(20)),
        };
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(string.Empty, truncated: false, path: "/tmp/unity.log", sizeBytes: 0),
        };
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(),
            timeProvider);

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-unclassified-timeout"),
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

    private static DaemonGuiSessionRegistrationWaitResult CreateSuccessfulWaitResult (DaemonSession session)
    {
        return DaemonGuiSessionRegistrationWaitResult.Success(
            session,
            IpcUnityEditorObservationTestFactory.Create(
                editorMode: DaemonEditorMode.Gui,
                projectFingerprint: session.ProjectFingerprint));
    }

}
