using MackySoft.Tests;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.UnityProject;

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

        var result = await executor.Execute(configuration, artifactPaths, CancellationToken.None);

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

        var result = await executor.Execute(configuration, artifactPaths, CancellationToken.None);

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

        var result = await executor.Execute(configuration, artifactPaths, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ArtifactMissing, result.FailureKind);
        Assert.Contains("results.xml", result.ErrorMessage, StringComparison.Ordinal);
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
            Mode: "oneshot",
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.EditMode,
            RawTestPlatform: "editmode",
            BuildTarget: null,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TestSettingsPath: null,
            TimeoutSeconds: 1800);
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

        public Task<ProcessRunResult> RunAsync (
            ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }
}