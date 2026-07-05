using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandPreDispatchTests
{
    [Theory]
    [InlineData("unknown", null, null)]
    [InlineData(null, "not-an-int", null)]
    [InlineData(null, null, "xml")]
    [Trait("Size", "Small")]
    public async Task Run_WhenNormalizedOptionIsInvalid_ReturnsInvalidArgumentWithoutCallingService (
        string? mode,
        string? timeout,
        string? format)
    {
        var service = new RecordingBuildService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            mode: mode,
            timeout: timeout,
            format: format,
            cancellationToken: CancellationToken.None));

        BuildRunCommandAssert.InvalidArgumentReturnedWithoutBuildExecution(
            result,
            service);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public async Task Run_WithoutProfilePath_ReturnsInvalidArgumentWithoutCallingService (string? profilePath)
    {
        var service = new RecordingBuildService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: profilePath,
            cancellationToken: CancellationToken.None));

        BuildRunCommandAssert.InvalidArgumentReturnedWithoutBuildExecution(
            result,
            service);
    }
}
