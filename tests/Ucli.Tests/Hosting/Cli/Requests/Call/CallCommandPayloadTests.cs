using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CallCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_WhenPostReadSourceExists_WritesTopLevelPayload ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreatePostReadSourceResult()));
        var preflightService = new RecordingCallCommandPreflightService((_, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new CallCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.CallAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("postReadSource", postReadSource => postReadSource
                .HasInt32("schemaVersion", 1)
                .HasArrayLength("steps", 1)
                .HasProperty("steps", 0, step => step
                    .HasString("opId", "step-1")
                    .HasString("sourceKind", ContractLiteralCodec.ToValue(IpcExecutePostReadSourceKind.Edit))
                    .HasBoolean("playModeMutation", false)
                    .HasString("commit", ContractLiteralCodec.ToValue(IpcExecutePostReadCommit.Context))
                    .HasBoolean("persistenceExpected", true)
                    .HasString("expectedPostState", ContractLiteralCodec.ToValue(IpcExecuteExpectedPostState.Deterministic))));
    }
}
