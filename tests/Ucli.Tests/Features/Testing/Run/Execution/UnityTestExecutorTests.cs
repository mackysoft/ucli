using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

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

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(0)),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ProcessExitCode);
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

        var result = await executor.Execute(
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
    public async Task Execute_WhenUnityReportsProjectAlreadyOpenInEditorLog_ReturnsProjectAlreadyOpen ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "already-open-log");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile("run/editor.log", "It looks like another Unity instance is running with this project open.");

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(1, "Unity exited.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await executor.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ProjectAlreadyOpen, result.FailureKind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, result.ErrorCode);
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

        var result = await executor.Execute(
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

        var result = await executor.Execute(
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

        var result = await executor.Execute(
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

        var result = await executor.Execute(
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

        var result = await executor.Execute(
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

        var result = await executor.Execute(
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

        var result = await executor.Execute(
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

    private sealed class StubProjectLifecycleLockProvider : IProjectLifecycleLockProvider
    {
        private readonly bool throwTimeout;

        public StubProjectLifecycleLockProvider (bool throwTimeout = false)
        {
            this.throwTimeout = throwTimeout;
        }

        public ValueTask<IAsyncDisposable> Acquire (
            string storageRoot,
            string projectFingerprint,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (throwTimeout)
            {
                throw new TimeoutException("lock held");
            }

            return ValueTask.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());
        }
    }

    private sealed class StubUnityProjectLockFileProbe : IUnityProjectLockFileProbe
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
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync ()
        {
            return ValueTask.CompletedTask;
        }
    }
}
