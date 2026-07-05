using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class VerifyCommandPreDispatchTests
{
    [Theory]
    [InlineData("unknown", null, null)]
    [InlineData(null, "not-an-int", null)]
    [InlineData(null, null, "yaml")]
    [Trait("Size", "Small")]
    public async Task Verify_WhenNormalizedOptionIsInvalid_ReturnsInvalidArgumentWithoutCallingService (
        string? mode,
        string? timeout,
        string? format)
    {
        var service = new RecordingVerifyService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            mode: mode,
            timeout: timeout,
            format: format,
            cancellationToken: CancellationToken.None));

        VerifyCommandAssert.InvalidArgumentReturnedWithoutVerifyExecution(
            result,
            service);
    }
}
