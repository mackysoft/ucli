using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ValidateCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingValidateService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ValidateCommand(service, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ValidateAsync(
            timeout: "0",
            cancellationToken: CancellationToken.None));

        ValidateCommandAssert.InvalidArgumentRejectedBeforeValidation(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenReadIndexModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingValidateService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ValidateCommand(service, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ValidateAsync(
            readIndexMode: "unsupported",
            cancellationToken: CancellationToken.None));

        ValidateCommandAssert.InvalidArgumentRejectedBeforeValidation(
            result,
            service);
    }
}
