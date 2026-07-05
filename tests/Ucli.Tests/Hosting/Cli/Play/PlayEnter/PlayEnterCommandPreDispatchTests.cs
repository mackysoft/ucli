using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayEnterCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WithInvalidTimeout_ReturnsInvalidArgumentBeforeServiceExecution ()
    {
        var service = new RecordingPlayEnterService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EnterAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        PlayCommandAssert.InvalidTimeoutRejectedBeforeEnterExecution(
            result,
            service);
    }
}
