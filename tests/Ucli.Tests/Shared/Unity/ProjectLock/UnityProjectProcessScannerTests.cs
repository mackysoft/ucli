using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectProcessScannerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_WhenPsOutputContainsQuotedProjectPath_ReturnsMatchingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "quoted-project-path");
        var projectPath = scope.CreateDirectory("Unity Project");
        var processOutput = $"  12345 /Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath \"{projectPath}\" -logFile /tmp/unity.log\n";
        var scanner = new UnityProjectProcessScanner(new StubProcessRunner(ProcessRunResult.Exited(
            0,
            standardOutput: processOutput)));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Matches);
        Assert.Equal(12345, match.ProcessId);
        Assert.Equal(projectPath, match.ProjectPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_WhenProcessListCommandFails_ReturnsFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "scan-failed");
        var projectPath = scope.CreateDirectory("UnityProject");
        var scanner = new UnityProjectProcessScanner(new StubProcessRunner(ProcessRunResult.StartFailed("ps denied")));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("ps denied", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TokenizeCommandLine_WhenPathContainsSpaces_PreservesProjectPathToken ()
    {
        var tokens = UnityProjectProcessScanner.TokenizeCommandLine(
            "/Applications/Unity.app/Contents/MacOS/Unity -projectPath '/tmp/Unity Project' -batchmode");

        Assert.Contains("-projectPath", tokens);
        Assert.Contains("/tmp/Unity Project", tokens);
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
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }
}
