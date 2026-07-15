using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;
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
    public async Task Resolve_WhenGlobalObjectIdUsesEquivalentText_CanonicalizesTypedSelector ()
    {
        var service = new RecordingResolveService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            globalObjectId: "GlobalObjectId_V1-02-0123456789ABCDEF0123456789ABCDEF-0004-0005",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        var selector = Assert.IsType<ResolveGlobalObjectIdSelectorInput>(invocation.Input.Selector);
        Assert.Equal(GlobalObjectId, selector.GlobalObjectId.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenAssetGuidUsesUppercaseNFormat_ParsesNativeGuid ()
    {
        var service = new RecordingResolveService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            assetGuid: "0123456789ABCDEF0123456789ABCDEF",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        var selector = Assert.IsType<ResolveAssetGuidSelectorInput>(invocation.Input.Selector);
        Assert.Equal(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), selector.AssetGuid);
    }

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
