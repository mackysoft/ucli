using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ReadyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCommandResultTests
{
    [Theory]
    [InlineData(AssuranceVerdict.Fail)]
    [InlineData(AssuranceVerdict.Incomplete)]
    [Trait("Size", "Small")]
    public async Task Ready_WithNonPassVerdict_ReturnsOkEnvelopeWithFailureExitCode (AssuranceVerdict verdict)
    {
        var service = new RecordingReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput(verdict))));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "execution",
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Ready,
            ContractLiteralCodec.ToValue(CommandResultStatus.Ok),
            1);
        Assert.Equal(
            ContractLiteralCodec.ToValue(verdict),
            outputJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }
}
