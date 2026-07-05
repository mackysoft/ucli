using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ReadyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "execution",
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        ReadyCommandAssert.ExecutionTargetDispatchedWithOptions(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 1234,
            expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WithReadIndexTarget_MapsReadIndexModeToServiceInput ()
    {
        var service = new RecordingReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "readIndex",
            readIndexMode: "requireFresh",
            cancellationToken: CancellationToken.None));

        ReadyCommandAssert.ReadIndexTargetDispatchedWithReadIndexMode(
            result,
            service,
            ReadIndexMode.RequireFresh);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WhenTargetIsOmitted_UsesExecutionTarget ()
    {
        var service = new RecordingReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: null,
            cancellationToken: CancellationToken.None));

        ReadyCommandAssert.DefaultExecutionTargetDispatched(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WhenTargetIsBlank_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingReadyService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "   ",
            cancellationToken: CancellationToken.None));

        ReadyCommandAssert.InvalidTargetRejectedBeforeReadyExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WhenTargetIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingReadyService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "unknown",
            cancellationToken: CancellationToken.None));

        ReadyCommandAssert.InvalidTargetRejectedBeforeReadyExecution(
            result,
            service);
    }
}
