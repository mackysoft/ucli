using MackySoft.Tests;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class WindowsDetachedSupervisorProcessLauncherTests
{
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