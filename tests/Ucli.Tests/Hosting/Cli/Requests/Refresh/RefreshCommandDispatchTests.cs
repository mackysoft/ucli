using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.RefreshCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingRefreshService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        RefreshCommandAssert.SucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            expectedFailFast: true);
    }
}
