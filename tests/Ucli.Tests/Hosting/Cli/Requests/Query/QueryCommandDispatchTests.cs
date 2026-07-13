using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.QueryCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class QueryCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GoDescribe_WhenGlobalObjectIdUsesEquivalentText_DispatchesCanonicalTypedTarget ()
    {
        var service = new RecordingQueryService((_, _) => ValueTask.FromResult(CreateSuccessResult(UcliCommandNames.QueryGoDescribe)));
        var command = new QueryGoDescribeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.DescribeAsync(
            globalObjectId: "GlobalObjectId_V1-02-0123456789ABCDEF0123456789ABCDEF-0004-0005",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        var operation = Assert.IsType<QueryUnityOperationRequest>(invocation.Input.Operation);
        JsonAssert.For(operation.Args)
            .HasProperty("target", target => target
                .HasString("globalObjectId", "GlobalObjectId_V1-2-0123456789abcdef0123456789abcdef-4-5"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetSchema_WhenAssetGuidUsesUppercaseNFormat_DispatchesCanonicalTypedTarget ()
    {
        var service = new RecordingQueryService((_, _) => ValueTask.FromResult(CreateSuccessResult(UcliCommandNames.QueryAssetSchema)));
        var command = new QueryAssetSchemaCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.SchemaAsync(
            assetGuid: "0123456789ABCDEF0123456789ABCDEF",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        var operation = Assert.IsType<QueryUnityOperationRequest>(invocation.Input.Operation);
        JsonAssert.For(operation.Args)
            .HasProperty("target", target => target
                .HasString("assetGuid", "0123456789abcdef0123456789abcdef"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetsFind_MapsOptionsToQueryOperationAndCancellationToken ()
    {
        var service = new RecordingQueryService((_, _) => ValueTask.FromResult(CreateSuccessResult(UcliCommandNames.QueryAssetsFind)));
        var command = new QueryAssetsFindCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.FindAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "allowStale",
            failFast: true,
            type: "UnityEngine.Material, UnityEngine.CoreModule",
            limit: 50,
            cancellationToken: cancellationTokenSource.Token));

        QueryCommandAssert.AssetsFindSucceededWithDispatchedOperation(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            ReadIndexMode.AllowStale,
            expectedFailFast: true,
            expectedTypeId: "UnityEngine.Material, UnityEngine.CoreModule",
            expectedLimit: 50);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SceneTree_MapsOptionsToQueryOperationAndWindowCursor ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(3);
        var service = new RecordingQueryService((_, _) => ValueTask.FromResult(CreateSuccessResult(UcliCommandNames.QuerySceneTree)));
        var command = new QuerySceneTreeCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.TreeAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "allowStale",
            failFast: true,
            path: "Assets/Scenes/Main.unity",
            depth: 2,
            limit: 25,
            after: cursor,
            cancellationToken: cancellationTokenSource.Token));

        QueryCommandAssert.SceneTreeSucceededWithDispatchedOperation(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            ReadIndexMode.AllowStale,
            expectedFailFast: true,
            expectedScenePath: "Assets/Scenes/Main.unity",
            expectedDepth: 2,
            expectedLimit: 25,
            expectedCursor: cursor,
            expectedOffset: 3);
    }
}
