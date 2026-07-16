using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.PlanCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class PlanCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenReadIndexModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingPlanService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var preflightService = new UnexpectedPlanCommandPreflightService();
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            readIndexMode: "unsupported",
            cancellationToken: CancellationToken.None));

        PlanCommandAssert.InvalidArgumentReturnedWithoutPlanExecution(
            result,
            service);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("disabled")]
    [InlineData("allowStale")]
    [InlineData("requireFresh")]
    public async Task Plan_WhenAllowPlayModeAndReadIndexModeAreSpecified_ReturnsInvalidArgumentWithoutCallingService (
        string readIndexMode)
    {
        var service = new RecordingPlanService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var preflightService = new UnexpectedPlanCommandPreflightService();
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            readIndexMode: readIndexMode,
            allowPlayMode: true,
            cancellationToken: CancellationToken.None));

        PlanCommandAssert.InvalidArgumentReturnedWithoutPlanExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenTimeoutIsInvalid_ReturnsPreflightRequestIdWithoutExecutingPlan ()
    {
        var service = new RecordingPlanService((_, _) => throw new InvalidOperationException("Execute should not be called."));
        var preflightService = new RecordingPlanCommandPreflightService((_, _, _, _) => ValueTask.FromResult(PlanCommandPreflightResult.Success(
            CreatePreflightOutput())));
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.NotEqual(Guid.Empty, Assert.Single(preflightService.Invocations).RequestId);
        PlanCommandAssert.InvalidArgumentReturnedWithoutPlanExecution(
            result,
            service,
            expectedRequestId: RequestId);
    }
}
