using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ReadyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCommandResultTests
{
    [Theory]
    [InlineData(ReadyVerdictValues.Fail)]
    [InlineData(ReadyVerdictValues.Incomplete)]
    [Trait("Size", "Small")]
    public async Task Ready_WithNonPassVerdict_ReturnsOkEnvelopeWithFailureExitCode (string verdict)
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
            IpcProtocol.StatusOk,
            1);
        Assert.Equal(verdict, outputJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }
}
