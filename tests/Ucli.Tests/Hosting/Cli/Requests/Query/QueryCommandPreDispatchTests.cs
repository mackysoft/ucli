using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class QueryCommandPreDispatchTests
{
    private const string GlobalObjectId = "GlobalObjectId_V1-2-0123456789abcdef0123456789abcdef-4-5";

    [Fact]
    [Trait("Size", "Small")]
    public async Task GoDescribe_WhenGlobalObjectIdIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryGoDescribeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.DescribeAsync(
            globalObjectId: "not-a-global-object-id",
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryGoDescribe);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetSchema_WhenAssetGuidIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryAssetSchemaCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.SchemaAsync(
            assetGuid: "not-an-asset-guid",
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryAssetSchema);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetSchema_WhenGlobalObjectIdIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryAssetSchemaCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.SchemaAsync(
            globalObjectId: "not-a-global-object-id",
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryAssetSchema);
    }

    [Theory]
    [InlineData("Assets/Scenes/Main.prefab", "Root", null)]
    [InlineData(null, "Root", "Assets/Prefabs/Player.unity")]
    [InlineData("Assets/Scenes/Main.unity", "Root//Child", null)]
    [Trait("Size", "Small")]
    public async Task GoDescribe_WhenHierarchyTargetPathIsInvalid_ReturnsInvalidArgumentWithoutCallingService (
        string? scene,
        string hierarchyPath,
        string? prefab)
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryGoDescribeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.DescribeAsync(
            scene: scene,
            hierarchyPath: hierarchyPath,
            prefab: prefab,
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryGoDescribe);
    }

    [Theory]
    [InlineData("Assets", null)]
    [InlineData(null, "Assets/TagManager.asset")]
    [Trait("Size", "Small")]
    public async Task AssetSchema_WhenAssetPathIsInvalid_ReturnsInvalidArgumentWithoutCallingService (
        string? assetPath,
        string? projectAssetPath)
    {
        var service = new RecordingQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryAssetSchemaCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.SchemaAsync(
            assetPath: assetPath,
            projectAssetPath: projectAssetPath,
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryAssetSchema);
    }

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
            globalObjectId: GlobalObjectId,
            scene: "Assets/Scenes/Main.unity",
            hierarchyPath: "Root",
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.InvalidQueryInputRejectedBeforeExecution(
            result,
            service,
            UcliCommandNames.QueryGoDescribe);
    }
}
