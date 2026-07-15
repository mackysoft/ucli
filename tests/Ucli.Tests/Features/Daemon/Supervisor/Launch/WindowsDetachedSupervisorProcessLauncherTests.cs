using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class WindowsDetachedSupervisorProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildStartInfo_AppendsInternalSupervisorInvocationArguments ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "windows-detached-supervisor-launcher", "start-info");
        var normalizedStorageRoot = Path.GetFullPath(storageRoot);
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var startInfo = WindowsDetachedSupervisorProcessLauncher.BuildStartInfo(storageRoot, launchCommand);

        Assert.Equal("ucli", startInfo.FileName);
        Assert.Equal(normalizedStorageRoot, startInfo.WorkingDirectory);
        Assert.True(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(
            [
                "--base",
                ..SupervisorInvocationArguments.Build(normalizedStorageRoot),
            ],
            startInfo.ArgumentList);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Launch_WhenProcessStartFails_ReturnsStructuredError ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("windows-detached-supervisor-launcher", "start-failure");
        var launcher = new WindowsDetachedSupervisorProcessLauncher();

        var result = launcher.Launch(
            scope.FullPath,
            new SupervisorLaunchCommand(
                FileName: Path.Combine(scope.FullPath, "missing-ucli-binary"),
                Arguments: Array.Empty<string>()));

        Assert.NotNull(result);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Kind);
    }
}
