using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Call.Preflight;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CallCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_WhenModeIsInvalid_ReturnsPreflightPayloadWithoutExecutingCall ()
    {
        var service = new RecordingCallService((_, _) => throw new InvalidOperationException("Execute should not be called."));
        var preflightService = new RecordingCallCommandPreflightService((_, _, _) => ValueTask.FromResult(CallCommandPreflightResult.Success(
            CreatePreflightOutput())));
        var command = new CallCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.CallAsync(
            mode: "unsupported",
            cancellationToken: CancellationToken.None));

        CallCommandAssert.InvalidModePreparedPayloadWithoutCallExecution(
            result,
            service,
            preflightService,
            DefaultRequestJson,
            expectedRequestId: RequestId);
    }
}
