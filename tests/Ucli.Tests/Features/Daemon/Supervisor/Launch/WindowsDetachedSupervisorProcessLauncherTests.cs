using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Invocation;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class WindowsDetachedSupervisorProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildStartInfo_AppendsInternalSupervisorInvocationArguments ()
    {
        using var scope = TestDirectories.CreateTempScope("windows-detached-supervisor-launcher", "start-info");
        var normalizedStorageRoot = Path.GetFullPath(scope.FullPath);
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var startInfo = WindowsDetachedSupervisorProcessLauncher.BuildStartInfo(scope.FullPath, launchCommand);

        Assert.Equal("ucli", startInfo.FileName);
        Assert.Equal(normalizedStorageRoot, startInfo.WorkingDirectory);
        Assert.True(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(
            [
                "--base",
                SupervisorInvocationArguments.InternalServeFlag,
                SupervisorInvocationArguments.RepositoryRootOption,
                normalizedStorageRoot,
            ],
            startInfo.ArgumentList);
    }

    [Fact]
    [Trait("Size", "Small")]
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
