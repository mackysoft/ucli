using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class RequestReadIndexAccessInvocationAssert
{
    public static void AssetsFindServedByAssetLookupOnly (
        QueryServiceResult result,
        RecordingAssetSearchLookupAccessService assetSearchLookupAccessService,
        RecordingSceneTreeLiteAccessService sceneTreeLiteAccessService,
        string expectedCommandName,
        string expectedTypeId,
        bool expectedFailFast)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Equal(expectedCommandName, result.CommandName);
        AssetSearchRequestedOnce(
            assetSearchLookupAccessService,
            expectedTypeId,
            expectedFailFast);
        Assert.Empty(sceneTreeLiteAccessService.Invocations);
        Assert.True(result.ReadIndex.Used);
        Assert.Equal(ReadIndexInfoSource.Index, result.ReadIndex.Source);
    }

    public static void SceneTreeServedBySceneTreeLiteOnly (
        QueryServiceResult result,
        RecordingAssetSearchLookupAccessService assetSearchLookupAccessService,
        RecordingSceneTreeLiteAccessService sceneTreeLiteAccessService,
        UcliCommand expectedCommand,
        string expectedScenePath,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(assetSearchLookupAccessService.Invocations);
        SceneTreeRequestedOnce(
            sceneTreeLiteAccessService,
            expectedCommand,
            expectedScenePath,
            expectedReadIndexMode,
            expectedFailFast);
    }

    public static void UnityOnlyQueryBypassedReadIndexAccess (
        QueryServiceResult result,
        RecordingAssetSearchLookupAccessService assetSearchLookupAccessService,
        RecordingSceneTreeLiteAccessService sceneTreeLiteAccessService)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(assetSearchLookupAccessService.Invocations);
        Assert.Empty(sceneTreeLiteAccessService.Invocations);
        Assert.Equal("query operation is not backed by readIndex.", result.ReadIndex.FallbackReason);
    }

    public static void ResolveSelectorBypassedSceneTreeLiteAccess (
        ResolveServiceResult result,
        RecordingSceneTreeLiteAccessService sceneTreeLiteAccessService)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(sceneTreeLiteAccessService.Invocations);
        Assert.False(result.ReadIndex.Used);
        Assert.Equal(ReadIndexInfoSource.Unity, result.ReadIndex.Source);
        Assert.Equal(IndexFreshness.Fresh, result.ReadIndex.Freshness);
        Assert.Equal("selector requires live Unity resolution.", result.ReadIndex.FallbackReason);
    }

    public static RecordingAssetSearchLookupAccessService.Invocation AssetSearchRequestedOnce (
        RecordingAssetSearchLookupAccessService accessService,
        string expectedTypeId,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(accessService.Invocations);
        Assert.Equal(expectedTypeId, invocation.Query.TypeId!.Value);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        return invocation;
    }

    public static RecordingSceneTreeLiteAccessService.Invocation SceneTreeRequestedOnce (
        RecordingSceneTreeLiteAccessService accessService,
        UcliCommand expectedCommand,
        string expectedScenePath,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(accessService.Invocations);
        Assert.Equal(expectedCommand, invocation.Command);
        Assert.Equal(expectedScenePath, invocation.ScenePath.Value);
        if (SceneAssetPath.TryParse(expectedScenePath, out var expectedIndexScenePath))
        {
            Assert.Equal(expectedIndexScenePath, invocation.IndexScenePath);
        }
        else
        {
            Assert.Null(invocation.IndexScenePath);
        }

        Assert.Equal(expectedReadIndexMode, invocation.ReadIndexMode);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        return invocation;
    }

    public static RecordingAssetLookupSourceRefreshService.Invocation AssetLookupRefreshRequestedOnce (
        RecordingAssetLookupSourceRefreshService refreshService,
        UcliCommand expectedCommand,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(refreshService.Invocations);
        Assert.Equal(expectedCommand, invocation.Command);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        return invocation;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteOperationInvocation UnityOperationRequestedOnce (
        RecordingUnityRequestExecutor requestExecutor,
        UcliCommand expectedCommand,
        UnityExecutionMode expectedMode,
        TimeSpan expectedTimeout,
        bool expectedFailFast,
        string expectedOperationId,
        string expectedOperationName)
    {
        var execution = UnityRequestExecutorInvocationAssert.ExecuteOperationOnce(
            requestExecutor.Invocations,
            expectedCommand,
            expectedCommand);

        Assert.Equal(expectedMode, execution.Invocation.Mode);
        Assert.Equal(expectedTimeout, execution.Invocation.Timeout);
        Assert.Equal(expectedFailFast, execution.Request.FailFast);
        Assert.Equal(expectedOperationId, execution.Request.OperationId.Value);
        Assert.Equal(expectedOperationName, execution.Request.OperationName);
        return execution;
    }
}
