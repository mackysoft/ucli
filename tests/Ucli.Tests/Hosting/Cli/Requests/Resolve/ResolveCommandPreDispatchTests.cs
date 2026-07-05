using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class ResolveCommandPreDispatchTests
{
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
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
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
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
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
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        ResolveCommandAssert.InvalidInputRejectedBeforeResolveExecution(
            result,
            service);
    }
}
