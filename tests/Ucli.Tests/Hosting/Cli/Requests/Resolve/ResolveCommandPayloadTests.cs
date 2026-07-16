using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ResolveCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ResolveCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithSuccessResult_WritesResolvedObjectPayload ()
    {
        var service = new RecordingResolveService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            scene: "Assets/Scenes/Main.unity",
            hierarchyPath: "Root/Child",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.SucceededWithPayload(
            result,
            expectedRequestId: RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenServiceFails_PreservesFailurePayloadAndErrors ()
    {
        var service = new RecordingResolveService((_, _) => ValueTask.FromResult(CreateFailureResult()));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            globalObjectId: GlobalObjectId,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Resolve,
            ContractLiteralCodec.ToValue(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "Unity execution failed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", RequestId)
                .HasArrayLength("opResults", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasString("source", "index")
                    .HasString("freshness", "fresh")))
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", UcliCoreErrorCodes.InternalError.Value)
                .HasString("message", "Unity execution failed.")
                .HasString("opId", "resolve"));
    }
}
