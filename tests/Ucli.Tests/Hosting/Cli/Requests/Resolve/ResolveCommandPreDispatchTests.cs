using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ResolveCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ResolveCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenGlobalObjectIdIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            globalObjectId: "not-a-global-object-id",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenAssetGuidIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            assetGuid: "not-an-asset-guid",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }

    [Theory]
    [InlineData(null, "Assets", null, null, null)]
    [InlineData(null, null, "Assets/TagManager.asset", null, null)]
    [InlineData("Assets/Scenes/Main.prefab", null, null, "Root", null)]
    [InlineData(null, null, null, "Root", "Assets/Prefabs/Player.unity")]
    [InlineData("Assets/Scenes/Main.unity", null, null, "Root//Child", null)]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenPathSelectorIsInvalid_ReturnsInvalidArgumentWithoutCallingService (
        string? scene,
        string? assetPath,
        string? projectAssetPath,
        string? hierarchyPath,
        string? prefab)
    {
        var service = new RecordingResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            assetPath: assetPath,
            projectAssetPath: projectAssetPath,
            scene: scene,
            hierarchyPath: hierarchyPath,
            prefab: prefab,
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSelectorIsNotExactlyOne_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            assetGuid: "11111111111111111111111111111111",
            assetPath: "Assets/Example.asset",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenReadIndexModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            globalObjectId: GlobalObjectId,
            readIndexMode: "unsupported",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            globalObjectId: GlobalObjectId,
            mode: "unsupported",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ResolveAsync(
            globalObjectId: GlobalObjectId,
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }
}
