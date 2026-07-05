using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests;

namespace MackySoft.Tests;

internal static class EvalCommandAssert
{
    public static void SnippetRequestSucceededWithDispatch (
        CommandExecutionResult result,
        RecordingCallService service,
        RecordingEvalSourceInputReader sourceReader,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        string expectedSource)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        EvalSourceInputReaderAssert.SnippetRead(
            sourceReader,
            expectedSource,
            expectedCancellationToken);
        var input = CallServiceDispatchAssert.DispatchedWithOptions(
            service,
            expectedCancellationToken,
            expectedProjectPath,
            expectedMode,
            expectedTimeoutMilliseconds,
            expectedPlanToken: null,
            expectedWithPlan: true,
            expectedAllowDangerous: true,
            expectedAllowPlayMode: true,
            expectedFailFast: true,
            expectedRequestJson: null,
            UcliCommandIds.Eval);
        HasEvalRequestSource(input.RequestJson, expectedSource);
    }

    public static void SucceededWithPayload (
        CommandExecutionResult result,
        string expectedRequestId)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI eval completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", expectedRequestId)
                .HasValueKind("project", JsonValueKind.Object)
                .HasArrayLength("opResults", 1)
                .HasProperty("plan", plan => plan
                    .HasString("requestId", expectedRequestId)
                    .HasValueKind("project", JsonValueKind.Object)
                    .HasArrayLength("opResults", 1)
                    .HasString("planToken", "plan-token-1")));
        var planResult = outputJson.RootElement
            .GetProperty("payload")
            .GetProperty("plan")
            .GetProperty("opResults")[0]
            .GetProperty("result");
        Assert.False(planResult.TryGetProperty("returnValue", out _));
        Assert.False(planResult.TryGetProperty("logs", out _));
        Assert.False(planResult.TryGetProperty("durationMilliseconds", out _));
        Assert.False(planResult.TryGetProperty("touchedResources", out _));
    }

    public static void SucceededWithGolden (
        CommandExecutionResult result,
        string expectedRequestId)
    {
        SucceededWithPayload(
            result,
            expectedRequestId);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("eval", "success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    public static void FileSourceRequestSucceeded (
        CommandExecutionResult result,
        RecordingCallService service,
        RecordingEvalSourceInputReader sourceReader,
        string expectedFilePath,
        string expectedSource)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        EvalSourceInputReaderAssert.FileRead(sourceReader, expectedFilePath);
        var input = CallServiceDispatchAssert.DispatchedWithOptions(
            service,
            CancellationToken.None,
            expectedProjectPath: null,
            expectedMode: null,
            expectedTimeoutMilliseconds: null,
            expectedPlanToken: null,
            expectedWithPlan: true,
            expectedAllowDangerous: true,
            expectedAllowPlayMode: false,
            expectedFailFast: false,
            expectedRequestJson: null,
            UcliCommandIds.Eval);
        HasEvalRequestSource(input.RequestJson, expectedSource);
    }

    public static void DangerousExecutionDisallowedByDefault (
        CommandExecutionResult result,
        RecordingCallService service,
        RecordingEvalSourceInputReader sourceReader,
        string expectedSource)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        EvalSourceInputReaderAssert.SnippetRead(
            sourceReader,
            expectedSource,
            CancellationToken.None);
        var input = CallServiceDispatchAssert.DispatchedWithOptions(
            service,
            CancellationToken.None,
            expectedProjectPath: null,
            expectedMode: null,
            expectedTimeoutMilliseconds: null,
            expectedPlanToken: null,
            expectedWithPlan: true,
            expectedAllowDangerous: false,
            expectedAllowPlayMode: false,
            expectedFailFast: false,
            expectedRequestJson: null,
            UcliCommandIds.Eval);
        HasEvalRequestSource(input.RequestJson, expectedSource);
    }

    public static void SourceInputFailureReturnedBeforeCallExecution (
        CommandExecutionResult result,
        RecordingCallService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.Eval);
    }

    public static void InvalidModeRejectedBeforeSourceReadOrCallExecution (
        CommandExecutionResult result,
        RecordingCallService service,
        RecordingEvalSourceInputReader sourceReader)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);
        Assert.Empty(sourceReader.Invocations);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval);
    }

    private static void HasEvalRequestSource (
        string requestJson,
        string expectedSource)
    {
        using var document = JsonDocument.Parse(requestJson);
        JsonAssert.For(document.RootElement)
            .HasProperty("steps", 0, step => step
                .HasString("kind", "op")
                .HasString("id", "eval")
                .HasString("op", UcliPrimitiveOperationNames.CsEval)
                .HasProperty("args", args => args
                    .HasString("source", expectedSource)));
    }
}
