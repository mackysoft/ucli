using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class QueryCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetsFind_WhenWindowOptionsConflict_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryAssetsFindCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.FindAsync(
            type: "UnityEngine.Material, UnityEngine.CoreModule",
            limit: 10,
            all: true,
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryAssetsFind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SceneTree_WhenWindowOptionsConflict_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QuerySceneTreeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.TreeAsync(
            path: "Assets/Scenes/Main.unity",
            limit: 10,
            all: true,
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QuerySceneTree);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GoDescribe_WhenTargetIsAmbiguous_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryGoDescribeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.DescribeAsync(
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
            scene: "Assets/Scenes/Main.unity",
            hierarchyPath: "Root",
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryGoDescribe);
    }
}
