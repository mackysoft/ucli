using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.RefreshCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WithSuccessResult_WritesOperationPayload ()
    {
        var service = new RecordingRefreshService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            cancellationToken: CancellationToken.None));

        RefreshCommandAssert.SucceededWithPayload(
            result,
            expectedRequestId: RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenPostReadSourceExists_WritesTopLevelPayload ()
    {
        var service = new RecordingRefreshService((_, _) => ValueTask.FromResult(CreateSuccessResult(postReadSource: CreateRefreshPostReadSource())));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("postReadSource", postReadSource => postReadSource
                .HasInt32("schemaVersion", 1)
                .HasArrayLength("steps", 1)
                .HasProperty("steps", 0, step => step
                    .HasString("opId", "refresh")
                    .HasString("sourceKind", IpcExecutePostReadSourceKindNames.Refresh)
                    .HasBoolean("playModeMutation", false)
                    .HasValueKind("commit", JsonValueKind.Null)
                    .HasBoolean("persistenceExpected", true)
                    .HasString("expectedPostState", IpcExecuteExpectedPostStateNames.Unavailable)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenServiceFails_PreservesFailurePayloadAndErrors ()
    {
        var failureResult = OperationExecuteResultFactory.Failure(
            RequestGuid,
            [],
            [
                ApplicationFailure.InternalError(
                    "Unity execution failed.",
                    opId: "refresh"),
            ],
            "uCLI refresh failed.");
        var service = new RecordingRefreshService((_, _) => ValueTask.FromResult(failureResult));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "Unity execution failed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", RequestId)
                .HasArrayLength("opResults", 0))
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", UcliCoreErrorCodes.InternalError.Value)
                .HasString("message", "Unity execution failed.")
                .HasString("opId", "refresh"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenReadPostconditionExists_WritesTopLevelPayload ()
    {
        var readPostcondition = ReadPostconditionTestFactory.CreateAssetSearch();
        var service = new RecordingRefreshService((_, _) => ValueTask.FromResult(CreateSuccessResult(readPostcondition: readPostcondition)));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("readPostcondition", readPostconditionElement => readPostconditionElement
                .HasArrayLength("requirements", 1)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.AssetSearch)));
    }
}
