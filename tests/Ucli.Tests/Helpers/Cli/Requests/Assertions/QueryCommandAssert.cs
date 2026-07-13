using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Tests;

internal static class QueryCommandAssert
{
    public static void AssetsFindSucceededWithDispatchedOperation (
        CommandExecutionResult result,
        RecordingQueryService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast,
        string expectedTypeId,
        int expectedLimit)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var input = DispatchedInput(
            service,
            expectedCancellationToken,
            expectedProjectPath,
            expectedMode,
            expectedTimeoutMilliseconds,
            expectedReadIndexMode,
            expectedFailFast);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(input.Operation);
        Assert.Equal(UcliCommandNames.QueryAssetsFind, operation.CommandName);
        Assert.Equal("assets.find", operation.OperationId);
        Assert.Equal(UcliPrimitiveOperationNames.AssetsFind, operation.OperationName);
        Assert.Equal(expectedTypeId, operation.Filter.TypeId);
        Assert.Equal(expectedLimit, operation.WindowOptions.Limit);
    }

    public static void SceneTreeSucceededWithDispatchedOperation (
        CommandExecutionResult result,
        RecordingQueryService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast,
        string expectedScenePath,
        int expectedDepth,
        int expectedLimit,
        string expectedCursor,
        int expectedOffset)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var input = DispatchedInput(
            service,
            expectedCancellationToken,
            expectedProjectPath,
            expectedMode,
            expectedTimeoutMilliseconds,
            expectedReadIndexMode,
            expectedFailFast);
        var operation = Assert.IsType<QuerySceneTreeOperationRequest>(input.Operation);
        Assert.Equal(UcliCommandNames.QuerySceneTree, operation.CommandName);
        Assert.Equal("scene.tree", operation.OperationId);
        Assert.Equal(UcliPrimitiveOperationNames.SceneTree, operation.OperationName);
        Assert.Equal(expectedScenePath, operation.ScenePath);
        Assert.Equal(expectedDepth, operation.Depth);
        Assert.False(operation.WindowOptions.All);
        Assert.Equal(expectedLimit, operation.WindowOptions.Limit);
        Assert.Equal(expectedCursor, operation.WindowOptions.Cursor);
        Assert.Equal(expectedOffset, operation.WindowOptions.Offset);
    }

    public static void AssetsFindSucceededWithPayload (
        CommandExecutionResult result,
        string expectedRequestId)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QueryAssetsFind);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI query completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", expectedRequestId)
                .HasProperty("project", project => project
                    .HasString("projectPath", ProjectIdentityInfoTestFactory.DefaultProjectPath)
                    .HasString("projectFingerprint", ProjectIdentityInfoTestFactory.ProjectFingerprint.ToString())
                    .HasString("unityVersion", ProjectIdentityInfoTestFactory.UnityVersion))
                .HasArrayLength("opResults", 1)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasString("source", "index")));
    }

    public static void InvalidQueryInputRejectedBeforeExecution (
        CommandExecutionResult result,
        RecordingQueryService service,
        string commandName)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            commandName);
    }

    private static QueryCommandInput DispatchedInput (
        RecordingQueryService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast)
    {
        Assert.NotEqual(Guid.Empty, Assert.Single(service.RequestIds));
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedReadIndexMode, invocation.Input.ReadIndexMode);
        Assert.Equal(expectedFailFast, invocation.Input.FailFast);
        return invocation.Input;
    }
}
