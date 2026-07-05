using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayExitCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WithInvalidTimeout_ReturnsInvalidArgumentBeforeServiceExecution ()
    {
        var service = new RecordingPlayExitService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ExitAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        PlayCommandAssert.InvalidTimeoutRejectedBeforeExitExecution(
            result,
            service);
    }
}
