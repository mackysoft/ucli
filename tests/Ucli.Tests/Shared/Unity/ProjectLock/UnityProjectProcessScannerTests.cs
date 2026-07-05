using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectProcessScannerTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task FindProcessesForProject_WhenPsOutputContainsQuotedProjectPath_ReturnsMatchingProcess ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "quoted-project-path");
        var projectPath = scope.CreateDirectory("Unity Project");
        var processOutput = $"  12345 /Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath \"{projectPath}\" -logFile /tmp/unity.log\n";
        var scanner = new UnityProjectProcessScanner(new RecordingProcessRunner(ProcessRunResult.Exited(
            0,
            standardOutput: processOutput)));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Matches);
        Assert.Equal(12345, match.ProcessId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task FindProcessesForProject_WhenPsOutputContainsUnquotedProjectPathWithSpaces_ReturnsMatchingProcess ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "unquoted-project-path");
        var projectPath = scope.CreateDirectory("Unity Project");
        var processOutput = $"  12345 /Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath {projectPath} -logFile /tmp/unity.log\n";
        var scanner = new UnityProjectProcessScanner(new RecordingProcessRunner(ProcessRunResult.Exited(
            0,
            standardOutput: processOutput)));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Matches);
        Assert.Equal(12345, match.ProcessId);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
        var scanner = new UnityProjectProcessScanner(new RecordingProcessRunner(ProcessRunResult.Exited(
            0,
            standardOutput: processOutput)));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Matches);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task FindProcessesForProject_UsesAbsolutePsExecutablePath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "absolute-ps");
        var projectPath = scope.CreateDirectory("UnityProject");
        var processRunner = new RecordingProcessRunner(ProcessRunResult.Exited(0, standardOutput: string.Empty));
        var scanner = new UnityProjectProcessScanner(processRunner);

        _ = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        UnityProjectProcessScannerAssert.UsesAbsoluteUnixPsExecutable(processRunner);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task FindProcessesForProject_WhenProcessListCommandFails_ReturnsFailure ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "scan-failed");
        var projectPath = scope.CreateDirectory("UnityProject");
        var scanner = new UnityProjectProcessScanner(new RecordingProcessRunner(ProcessRunResult.StartFailed("ps denied")));

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("ps denied", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task FindProcessesForProject_WhenRunningOnWindows_UsesPowerShellProcessList ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("unity-project-process-scanner", "windows-process-list");
        var projectPath = scope.CreateDirectory("UnityProject");
        var processOutput = $"  12345\t\"C:\\Program Files\\Unity\\Hub\\Editor\\2023.2.22f1\\Editor\\Unity.exe\" -batchmode -projectPath \"{projectPath}\" -logFile editor.log\r\n";
        var processRunner = new RecordingProcessRunner(ProcessRunResult.Exited(
            0,
            standardOutput: processOutput));
        var scanner = new UnityProjectProcessScanner(processRunner);

        var result = await scanner.FindProcessesForProjectAsync(projectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Matches);
        Assert.Equal(12345, match.ProcessId);
        UnityProjectProcessScannerAssert.UsesWindowsPowerShellProcessList(processRunner);
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

}
