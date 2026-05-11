using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.TestDoubles;

namespace MackySoft.Ucli.Tests;

public sealed class UnityTestExecutorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithSuccessfulProcessAndArtifacts_ReturnsSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "success");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile("run/results.xml", "<test-run />");
        scope.WriteFile("run/editor.log", "log");
        var lockProvider = new StubProjectLifecycleLockProvider();
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0));

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            processRunner,
            lockProvider,
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ProcessExitCode);
        var lockRequest = Assert.IsType<ProjectLifecycleLockRequest>(lockProvider.LastRequest);
        Assert.Equal(configuration.UnityProject.UnityProjectRoot, lockRequest.UnityProjectRoot);
        Assert.NotNull(processRunner.LastRequest.TerminationPolicy);
        Assert.Equal(ProcessTerminationMode.GracefulThenKill, processRunner.LastRequest.TerminationPolicy!.Mode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithAbnormalExitCode_ReturnsFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "abnormal-exit");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(17, "Process exited with code 17.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.AbnormalExit, result.FailureKind);
        Assert.Contains("17", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStaleUnityLockFileAppearsAfterAbnormalExit_PreservesAbnormalExitAndReportsCleanup ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "abnormal-exit-lock-file");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(1, "Unity exited.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Unlocked(lockFilePath),
                UnityProjectLockFileProbeResult.Unlocked(lockFilePath),
                UnityProjectLockFileProbeResult.Locked(lockFilePath)));

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.AbnormalExit, result.FailureKind);
        Assert.Null(result.ErrorCode);
        Assert.Contains("Unity exited.", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Stale Unity project lock file was removed", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(lockFilePath, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAbnormalExitHasNoUnityLockFile_ReturnsAbnormalExit ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "abnormal-exit-unlocked");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(17, "Process exited with code 17.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.AbnormalExit, result.FailureKind);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMissingResultsXml_ReturnsArtifactMissingFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "missing-results-xml");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile("run/editor.log", "log");

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(0)),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ArtifactMissing, result.FailureKind);
        Assert.Contains("results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactsAreMissingAndEditorLogHasCompileError_ReturnsStructuredStartupFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "missing-results-compile-error");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile(
            "run/editor.log",
            """
            COMMAND LINE ARGUMENTS:
            Assets/Scripts/Broken.cs(10,5): error CS0246: The type or namespace name 'MissingType' could not be found
            Scripts have compiler errors.
            """);

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(0)),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ArtifactMissing, result.FailureKind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.NotNull(result.StartupFailure);
        var startupFailure = result.StartupFailure!;
        Assert.Equal("blocked", startupFailure.Startup!.StartupStatus);
        Assert.Equal("compile", startupFailure.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, startupFailure.Startup.ProcessAction);
        Assert.Equal("unityScriptCompilationFailed", startupFailure.Diagnosis!.Reason);
        Assert.Equal("CS0246", startupFailure.Diagnosis.PrimaryDiagnostic!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAbnormalExitHasCompileErrorLog_ReturnsStructuredStartupFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "abnormal-exit-compile-error");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile(
            "run/editor.log",
            """
            COMMAND LINE ARGUMENTS:
            Assets/Scripts/Broken.cs(10,5): error CS0246: The type or namespace name 'MissingType' could not be found
            Scripts have compiler errors.
            """);

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(1, "Unity exited.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.AbnormalExit, result.FailureKind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.NotNull(result.StartupFailure);
        Assert.Equal("CS0246", result.StartupFailure!.Diagnosis!.PrimaryDiagnostic!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenEditorLogExceedsTailLimit_ClassifiesStartupFailureFromTail ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "large-editor-log-compile-error");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile(
            "run/editor.log",
            new string('x', 70000) +
            """

            COMMAND LINE ARGUMENTS:
            Assets/Scripts/Broken.cs(10,5): error CS0246: The type or namespace name 'MissingType' could not be found
            Scripts have compiler errors.
            """);

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(0)),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ArtifactMissing, result.FailureKind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.NotNull(result.StartupFailure);
        Assert.Equal("compile", result.StartupFailure!.Startup!.StartupBlockingReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithSubSecondTimeout_PreservesExactTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "timeout-round-up");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile("run/results.xml", "<test-run />");
        scope.WriteFile("run/editor.log", "log");
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0));
        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            processRunner,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(1), processRunner.LastRequest.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCanceledBeforeProcessStart_ReturnsCanceledWithoutStartingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "canceled-before-start");
        using var cancellationTokenSource = new CancellationTokenSource();
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0));
        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"], cancellationTokenSource.Cancel),
            processRunner,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            cancellationTokenSource.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.Canceled, result.FailureKind);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProcessTimesOut_ReturnsProcessTimedOutFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "process-timeout");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.TimedOut("Unity process timed out.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ProcessTimedOut, result.FailureKind);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProcessTimesOutAndStaleUnityLockFileRemains_ReturnsTimeoutWithCleanupDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "process-timeout-stale-lock");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.TimedOut(
                "Unity process timed out.",
                terminationResult: ProcessTerminationResult.ForceKilled)),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Unlocked(lockFilePath),
                UnityProjectLockFileProbeResult.Unlocked(lockFilePath),
                UnityProjectLockFileProbeResult.Locked(lockFilePath)));

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ProcessTimedOut, result.FailureKind);
        Assert.Null(result.ErrorCode);
        Assert.Contains("Stale Unity project lock file was removed", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(lockFilePath, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProcessIsCanceledAndStaleUnityLockFileRemains_ReturnsCanceledWithCleanupDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "process-canceled-stale-lock");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Canceled(
                "Unity process was canceled.",
                terminationResult: ProcessTerminationResult.ForceKilled)),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Unlocked(lockFilePath),
                UnityProjectLockFileProbeResult.Unlocked(lockFilePath),
                UnityProjectLockFileProbeResult.Locked(lockFilePath)));

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.Canceled, result.FailureKind);
        Assert.Null(result.ErrorCode);
        Assert.Contains("Stale Unity project lock file was removed", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(lockFilePath, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProjectLifecycleLockIsHeld_ReturnsProjectAlreadyOpenWithoutStartingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "lifecycle-lock-held");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0));
        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            processRunner,
            new StubProjectLifecycleLockProvider(throwTimeout: true),
            new StubUnityProjectLockFileProbe());

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ProjectAlreadyOpen, result.FailureKind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, result.ErrorCode);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityLockFileExists_ReturnsProjectAlreadyOpenWithoutStartingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "unity-lock-file");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0));
        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            processRunner,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(UnityProjectLockFileProbeResult.Locked(scope.GetPath("UnityProject/Temp/UnityLockfile"))));

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ProjectAlreadyOpen, result.FailureKind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, result.ErrorCode);
        Assert.Contains("UnityLockfile", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStaleLockCleanupFailsBeforeStart_ReturnsCleanupFailedWithoutStartingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "unity-lock-cleanup-failed");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0));
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");
        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            processRunner,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockPreflightService(UnityProjectLockPreflightResult.CleanupFailed(
                lockFilePath,
                UnityProjectLockFailureMessage.CreateCleanupFailed(lockFilePath, "access denied"))));

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.StartFailed, result.FailureKind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectLockCleanupFailed, result.ErrorCode);
        Assert.Contains("access denied", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityLockFileAppearsBeforeProcessStart_ReturnsProjectAlreadyOpenWithoutStartingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "unity-lock-file-race");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0));
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");
        var lockFileProbe = new StubUnityProjectLockFileProbe(
            UnityProjectLockFileProbeResult.Unlocked(lockFilePath),
            UnityProjectLockFileProbeResult.Locked(lockFilePath));
        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            processRunner,
            new StubProjectLifecycleLockProvider(),
            lockFileProbe);

        var result = await executor.ExecuteAsync(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ProjectAlreadyOpen, result.FailureKind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, result.ErrorCode);
        Assert.Equal(2, lockFileProbe.CallCount);
        Assert.Equal(0, processRunner.CallCount);
    }

    private static ResolvedTestRunConfiguration CreateConfiguration (TestDirectoryScope scope)
    {
        var projectPath = scope.GetPath("UnityProject");
        return new ResolvedTestRunConfiguration(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: projectPath,
                RepositoryRoot: scope.FullPath,
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Mode: UnityExecutionMode.Oneshot,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TestSettingsPath: null,
            TimeoutMilliseconds: null);
    }

    private static ArtifactPaths CreateArtifactPaths (TestDirectoryScope scope)
    {
        return TestArtifactPaths.Create(scope.GetPath("run"));
    }

    private sealed class StubUnityCommandBuilder : IUnityCommandBuilder
    {
        private readonly IReadOnlyList<string> arguments;

        private readonly Action? onBuild;

        public StubUnityCommandBuilder (
            IReadOnlyList<string> arguments,
            Action? onBuild = null)
        {
            this.arguments = arguments;
            this.onBuild = onBuild;
        }

        public IReadOnlyList<string> BuildArguments (
            ResolvedTestRunConfiguration configuration,
            ArtifactPaths artifactPaths)
        {
            onBuild?.Invoke();
            return arguments;
        }
    }

    private sealed class StubProcessRunner : IProcessRunner
    {
        private readonly ProcessRunResult result;

        public StubProcessRunner (ProcessRunResult result)
        {
            this.result = result;
        }

        public ProcessRunRequest LastRequest { get; private set; } = null!;

        public int CallCount { get; private set; }

        public Task<ProcessRunResult> RunAsync (
            ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class StubUnityProjectLockFileProbe : IUnityProjectLockFileProbe, IUnityProjectLockPreflightService
    {
        private readonly IReadOnlyList<UnityProjectLockFileProbeResult> results;

        private int nextResultIndex;

        public StubUnityProjectLockFileProbe ()
            : this(UnityProjectLockFileProbeResult.Unlocked("/tmp/UnityLockfile"))
        {
        }

        public StubUnityProjectLockFileProbe (params UnityProjectLockFileProbeResult[] results)
        {
            this.results = results is { Length: > 0 }
                ? results
                : [UnityProjectLockFileProbeResult.Unlocked("/tmp/UnityLockfile")];
        }

        public int CallCount { get; private set; }

        public UnityProjectLockFileProbeResult Probe (string unityProjectRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);

            var resultIndex = Math.Min(nextResultIndex, results.Count - 1);
            nextResultIndex++;
            CallCount++;
            return results[resultIndex];
        }

        public ValueTask<UnityProjectLockPreflightResult> PrepareForUnityProcessStartAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ConvertProbeResult(Probe(unityProject.UnityProjectRoot), unityProject));
        }

        public ValueTask<UnityProjectLockPreflightResult> CleanupStaleLockAfterUnityProcessExitAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ConvertPostExitProbeResult(Probe(unityProject.UnityProjectRoot)));
        }

        private static UnityProjectLockPreflightResult ConvertProbeResult (
            UnityProjectLockFileProbeResult result,
            ResolvedUnityProjectContext unityProject)
        {
            if (!result.IsSuccess)
            {
                return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
            }

            if (!result.IsLocked)
            {
                return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
            }

            return UnityProjectLockPreflightResult.ActiveLock(
                result.LockFilePath!,
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, result.LockFilePath));
        }

        private static UnityProjectLockPreflightResult ConvertPostExitProbeResult (UnityProjectLockFileProbeResult result)
        {
            if (!result.IsSuccess)
            {
                return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
            }

            if (!result.IsLocked)
            {
                return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
            }

            return UnityProjectLockPreflightResult.StaleLockCleared(result.LockFilePath!);
        }
    }

    private sealed class StubUnityProjectLockPreflightService : IUnityProjectLockPreflightService
    {
        private readonly UnityProjectLockPreflightResult result;

        public StubUnityProjectLockPreflightService (UnityProjectLockPreflightResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityProjectLockPreflightResult> PrepareForUnityProcessStartAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }

        public ValueTask<UnityProjectLockPreflightResult> CleanupStaleLockAfterUnityProcessExitAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

}
