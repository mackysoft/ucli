using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCommandPreDispatchTests
{
    [Theory]
    [InlineData(null, "abc")]
    [InlineData("unsupported", null)]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenOptionIsInvalid_ReturnsInvalidArgumentWithoutCallingService (
        string? mode,
        string? timeout)
    {
        var service = new RecordingRefreshService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            mode: mode,
            timeout: timeout,
            cancellationToken: CancellationToken.None));

        RefreshCommandAssert.InvalidArgumentReturnedWithoutRefreshExecution(
            result,
            service);
    }
}
