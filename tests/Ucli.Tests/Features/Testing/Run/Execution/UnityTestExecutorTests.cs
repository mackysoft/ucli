using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Features.Testing.Run.Execution;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

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
            new StubProcessRunner(ProcessRunResult.Exited(0)));

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
            new StubProcessRunner(ProcessRunResult.Exited(17, "Process exited with code 17.")));

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
    public async Task Execute_WithMissingResultsXml_ReturnsArtifactMissingFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "missing-results-xml");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);
        scope.WriteFile("run/editor.log", "log");

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.Exited(0)));

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
            processRunner);

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
    public async Task Execute_WhenProcessTimesOut_ReturnsProcessTimedOutFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-test-executor", "process-timeout");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = CreateArtifactPaths(scope);

        var executor = new UnityTestExecutor(
            new StubUnityCommandBuilder(["-batchmode"]),
            new StubProcessRunner(ProcessRunResult.TimedOut("Unity process timed out.")));

        var result = await executor.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(3000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ProcessTimedOut, result.FailureKind);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
        return new ArtifactPaths(scope.GetPath("run"));
    }

    private sealed class StubUnityCommandBuilder : IUnityCommandBuilder
    {
        private readonly IReadOnlyList<string> arguments;

        public StubUnityCommandBuilder (IReadOnlyList<string> arguments)
        {
            this.arguments = arguments;
        }

        public IReadOnlyList<string> BuildArguments (
            ResolvedTestRunConfiguration configuration,
            ArtifactPaths artifactPaths)
        {
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

        public Task<ProcessRunResult> RunAsync (
            ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
