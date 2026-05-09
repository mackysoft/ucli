using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectProcessScannerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_WhenPsOutputContainsQuotedProjectPath_ReturnsMatchingProcess ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

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
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_WhenPsOutputContainsUnquotedProjectPathWithSpaces_ReturnsMatchingProcess ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "unquoted-project-path");
        var projectPath = scope.CreateDirectory("Unity Project");
        var processOutput = $"  12345 /Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath {projectPath} -logFile /tmp/unity.log\n";
        var scanner = new UnityProjectProcessScanner(new StubProcessRunner(ProcessRunResult.Exited(
            0,
            standardOutput: processOutput)));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Matches);
        Assert.Equal(12345, match.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_WhenTargetPathIsPrefixOfUnquotedProjectPath_DoesNotReturnMatch ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "prefix-project-path");
        var projectPath = scope.CreateDirectory("Unity");
        var otherProjectPath = scope.CreateDirectory("Unity Project");
        var processOutput = $"  12345 /Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath {otherProjectPath} -logFile /tmp/unity.log\n";
        var scanner = new UnityProjectProcessScanner(new StubProcessRunner(ProcessRunResult.Exited(
            0,
            standardOutput: processOutput)));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Matches);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_UsesAbsolutePsExecutablePath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "absolute-ps");
        var projectPath = scope.CreateDirectory("UnityProject");
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0, standardOutput: string.Empty));
        var scanner = new UnityProjectProcessScanner(processRunner);

        _ = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.NotNull(processRunner.LastRequest);
        Assert.StartsWith("/", processRunner.LastRequest.FileName, StringComparison.Ordinal);
        Assert.EndsWith("/ps", processRunner.LastRequest.FileName, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_WhenProcessListCommandFails_ReturnsFailure ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "scan-failed");
        var projectPath = scope.CreateDirectory("UnityProject");
        var scanner = new UnityProjectProcessScanner(new StubProcessRunner(ProcessRunResult.StartFailed("ps denied")));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("ps denied", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FindProcessesForProject_WhenProcessScanIsUnsupported_ReturnsFailureWithoutRunningProcessListCommand ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "unsupported-platform");
        var projectPath = scope.CreateDirectory("UnityProject");
        var processRunner = new StubProcessRunner(ProcessRunResult.Exited(0, standardOutput: string.Empty));
        var scanner = new UnityProjectProcessScanner(processRunner);

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not implemented for this operating system", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(processRunner.LastRequest);
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

        public ProcessRunRequest? LastRequest { get; private set; }

        public Task<ProcessRunResult> RunAsync (
            ProcessRunRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
