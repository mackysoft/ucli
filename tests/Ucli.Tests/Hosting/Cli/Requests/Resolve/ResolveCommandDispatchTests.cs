using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ResolveCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ResolveCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_MapsOptionsToSceneHierarchySelectorAndCancellationToken ()
    {
        var service = new RecordingResolveService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "allowStale",
            failFast: true,
            scene: "Assets/Scenes/Main.unity",
            hierarchyPath: "Root/Child",
            cancellationToken: cancellationTokenSource.Token));

        ResolveCommandAssert.SucceededWithSceneHierarchySelector(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            ReadIndexMode.AllowStale,
            expectedFailFast: true,
            expectedScene: "Assets/Scenes/Main.unity",
            expectedHierarchyPath: "Root/Child");
    }
}
