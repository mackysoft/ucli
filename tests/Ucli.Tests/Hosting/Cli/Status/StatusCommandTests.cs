using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Hosting.Cli.Status;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class StatusCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingStatusService((_, _) => ValueTask.FromResult(StatusExecutionResult.Success(
            new StatusExecutionOutput(
                DaemonStatus: DaemonStatusKind.NotRunning,
                UnityVersion: "6000.1.4f1",
                ServerVersion: null,
                LifecycleState: null,
                BlockingReason: null,
                CompileState: null,
                Generations: null,
                CanAcceptExecutionRequests: false,
                EditorMode: null,
                TimeoutMilliseconds: 1234,
                ObservedAtUtc: null,
                ActionRequired: null,
                PrimaryDiagnostic: null,
                PlayMode: null))));
        var command = new StatusCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(
            projectPath: "/repo/UnityProject",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        StatusCommandAssert.SucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            expectedTimeoutMilliseconds: 1234);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingStatusService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new StatusCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        StatusCommandAssert.InvalidTimeoutRejectedBeforeStatusExecution(
            result,
            service);
    }

}
