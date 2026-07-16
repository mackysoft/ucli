using System.Text.Json;

namespace MackySoft.Tests;

internal static class TestRunCommandAssert
{
    public static void InvalidInputReturnedWithoutExecution (
        CommandExecutionResult result,
        RecordingTestRunService service)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun);
        HasInvalidInputPayload(outputJson.RootElement);
    }

    public static void InvalidArgumentReturnedWithoutExecutionAndStandardError (
        CommandExecutionResult result,
        RecordingTestRunService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailureWithEmptyStandardError(
            result,
            service.Invocations,
            UcliCommandNames.TestRun);
    }

    public static void CanceledBeforeExecution (
        CommandExecutionResult result,
        RecordingTestRunService service)
    {
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Equal(string.Empty, result.StdErr);
        Assert.Empty(service.Invocations);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.TestRun,
            ContractLiteralCodec.ToValue(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ExecutionErrorCodes.Canceled);
    }

    private static void HasInvalidInputPayload (JsonElement rootElement)
    {
        JsonAssert.For(rootElement)
            .HasProperty("payload", payload => payload
                .IsNull("result")
                .HasString("errorKind", "invalidInput")
                .IsNull("runId")
                .IsNull("artifactsDir")
                .IsNull("summaryJsonPath"));
    }
}
