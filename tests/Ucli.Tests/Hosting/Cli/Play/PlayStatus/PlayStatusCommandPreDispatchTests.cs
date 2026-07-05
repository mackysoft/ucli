using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayStatusCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WithInvalidTimeout_ReturnsInvalidArgumentBeforeServiceExecution ()
    {
        var service = new RecordingPlayStatusService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new PlayStatusCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        PlayCommandAssert.InvalidTimeoutRejectedBeforeStatusExecution(
            result,
            service);
    }
}
