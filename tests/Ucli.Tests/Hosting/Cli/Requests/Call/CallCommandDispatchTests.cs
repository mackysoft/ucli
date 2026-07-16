using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CallCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var preflightService = new RecordingCallCommandPreflightService((_, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new CallCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.CallAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            planToken: "user-token",
            withPlan: true,
            allowDangerous: true,
            allowPlayMode: true,
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        CallCommandAssert.SucceededWithDispatchedRequest(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            expectedPlanToken: "user-token",
            expectedRequestJson: DefaultRequestJson);
    }
}
