namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Foundation;
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
            editorMode: "gui",
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ipc.sock",
            processId: 4321);
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Success(session),
        };
        var logReader = new UnexpectedUnityLogReader(
            "Session registration success should not read the Unity log.");
        var processIdentityAssessor = RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess();
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            processIdentityAssessor,
            TimeProvider.System);
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
            NextResult = DaemonGuiSessionRegistrationWaitResult.Success(DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                projectFingerprint: "fingerprint-gui-observer-session",
                editorMode: "gui",
                endpointTransportKind: "unixDomainSocket",
                endpointAddress: "/tmp/ipc.sock",
                processId: 4321)),
        };
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            new UnexpectedUnityLogReader("Session registration success should not read the Unity log."),
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(),
            TimeProvider.System);

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
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(),
            TimeProvider.System);

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-compiler"),
            processId: Environment.ProcessId,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile), result.Blocker!.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix), result.Blocker.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Blocker.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, result.Blocker.ActionRequired);
        Assert.Equal(processStartedAtUtc, result.Blocker.ProcessStartedAtUtc);
        Assert.NotNull(result.Blocker.PrimaryDiagnostic);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation), result.Blocker.StartupPhase);
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
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(),
            TimeProvider.System);

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext($"fingerprint-gui-observer-{expectedReason}"),
            processId: Environment.ProcessId,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedStartupBlockingReason), result.Blocker!.StartupBlockingReason);
        Assert.Equal(expectedReason, result.Blocker!.Reason);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedRetryDisposition), result.Blocker.RetryDisposition);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedStartupPhase), result.Blocker.StartupPhase);
        Assert.Equal(expectedActionRequired, result.Blocker.ActionRequired);
        Assert.Equal(expectedPrimaryDiagnosticKind, result.Blocker.PrimaryDiagnostic!.Kind);
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
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            processIdentityAssessor,
            TimeProvider.System);

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-exit"),
            processId: int.MaxValue,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.ProcessExit), result.Blocker!.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown), result.Blocker.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap, result.Blocker.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, result.Blocker.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit, result.Blocker.PrimaryDiagnostic!.Kind);
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
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            processIdentityAssessor,
            TimeProvider.System);

        var result = await observer.WaitForStartupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-exit-with-log"),
            processId: int.MaxValue,
            processStartedAtUtc: processStartedAtUtc,
            unityLogPath: "/tmp/unity.log",
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.NotNull(result.Blocker);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile), result.Blocker!.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix), result.Blocker.RetryDisposition);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, result.Blocker.Reason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, result.Blocker.PrimaryDiagnostic!.Kind);
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
        var logReader = new UnexpectedUnityLogReader(
            "An exhausted GUI startup deadline must not begin another Unity log read.");
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForStartup_WhenLogReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var logReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logReadCompletion = new TaskCompletionSource<UnityLogReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            NextResult = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("registration timeout")),
        };
        var logReader = new RecordingUnityLogReader
        {
            ReadAsyncHandler = (_, _, _, _) =>
            {
                logReadStarted.TrySetResult();
                return new ValueTask<UnityLogReadResult>(logReadCompletion.Task);
            },
        };
        var observer = new DaemonGuiStartupObserver(
            awaiter,
            logReader,
            RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(),
            timeProvider);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = observer.WaitForStartupAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-gui-observer-log-timeout"),
                processId: Environment.ProcessId,
                processStartedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero),
                unityLogPath: "/tmp/unity.log",
                timeout: timeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await logReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsSuccess);
            Assert.False(result.IsBlocked);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error.Code);
        }
        finally
        {
            logReadCompletion.TrySetResult(UnityLogReadResult.Success(
                string.Empty,
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 0));
        }
    }

}
