using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.QueryCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class QueryCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetsFind_WithSuccessResult_WritesQueryPayload ()
    {
        var service = new RecordingQueryService((_, _) => ValueTask.FromResult(CreateSuccessResult(UcliCommandNames.QueryAssetsFind)));
        var command = new QueryAssetsFindCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.FindAsync(
            type: "UnityEngine.Material, UnityEngine.CoreModule",
            cancellationToken: CancellationToken.None));

        QueryCommandAssert.AssetsFindSucceededWithPayload(
            result,
            expectedRequestId: RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SceneTree_WithSuccessResult_WritesSuccessEnvelope ()
    {
        var service = new RecordingQueryService((_, _) => ValueTask.FromResult(CreateSuccessResult(UcliCommandNames.QuerySceneTree)));
        var command = new QuerySceneTreeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.TreeAsync(
            path: "Assets/Scenes/Main.unity",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QuerySceneTree);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetsFind_WhenServiceFails_PreservesFailurePayloadAndErrors ()
    {
        var service = new RecordingQueryService((_, _) => ValueTask.FromResult(CreateFailureResult(UcliCommandNames.QueryAssetsFind)));
        var command = new QueryAssetsFindCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.FindAsync(
            type: "UnityEngine.Material, UnityEngine.CoreModule",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QueryAssetsFind,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "Unity execution failed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", RequestId)
                .HasArrayLength("opResults", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasString("source", "index")))
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", UcliCoreErrorCodes.InternalError.Value)
                .HasString("message", "Unity execution failed.")
                .HasString("opId", "assets.find"));
    }
}
