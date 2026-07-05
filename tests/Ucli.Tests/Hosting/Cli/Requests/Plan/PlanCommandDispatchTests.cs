using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.PlanCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class PlanCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingPlanService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var preflightService = new UnexpectedPlanCommandPreflightService();
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "disabled",
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        PlanCommandAssert.SucceededWithDispatchedRequest(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            ReadIndexMode.Disabled,
            expectedFailFast: true,
            DefaultRequestJson,
            expectedAllowPlayMode: false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenAllowPlayModeIsSpecified_PassesAllowPlayModeToService ()
    {
        var service = new RecordingPlanService((_, _) => ValueTask.FromResult(CreateAllowPlayModeSuccessResult()));
        var preflightService = new UnexpectedPlanCommandPreflightService();
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            allowPlayMode: true,
            cancellationToken: CancellationToken.None));

        PlanCommandAssert.SucceededWithAllowPlayModeInput(
            result,
            service);
    }
}
