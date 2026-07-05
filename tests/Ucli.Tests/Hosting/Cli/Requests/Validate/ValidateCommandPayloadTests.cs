using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ValidateCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WithSuccessResult_WritesSuccessEnvelope ()
    {
        var service = new RecordingValidateService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new ValidateCommand(service, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ValidateAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Validate);
    }
}
