using System.Text.Json;
using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class LogsCommandAssert
{
    public static void DaemonReadInvalidArgumentReturnedWithoutExecution (
        int exitCode,
        string standardOutput,
        string standardError,
        RecordingLogsDaemonService service)
    {
        ReadInvalidArgumentReturnedWithoutExecution(
            exitCode,
            standardOutput,
            standardError,
            service.Invocations,
            UcliCommandNames.LogsDaemonRead);
    }

    public static void UnityReadInvalidArgumentReturnedWithoutExecution (
        int exitCode,
        string standardOutput,
        string standardError,
        RecordingLogsUnityService service)
    {
        ReadInvalidArgumentReturnedWithoutExecution(
            exitCode,
            standardOutput,
            standardError,
            service.Invocations,
            UcliCommandNames.LogsUnityRead);
    }

    public static void UnityClearInvalidArgumentReturnedWithoutExecution (
        int exitCode,
        string standardOutput,
        RecordingLogsUnityClearService service)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Empty(service.Invocations);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.LogsUnityClear);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    private static void ReadInvalidArgumentReturnedWithoutExecution<TInvocation> (
        int exitCode,
        string standardOutput,
        string standardError,
        IReadOnlyCollection<TInvocation> serviceInvocations,
        string command)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Empty(serviceInvocations);

        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasInvalidArgumentError(commandResult.RootElement, command);
        HasEmptyErrorCompletionPayload(commandResult.RootElement);
    }

    private static void HasEmptyErrorCompletionPayload (JsonElement rootElement)
    {
        var payload = rootElement.GetProperty("payload");
        Assert.Equal(0, payload.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("nextCursor").ValueKind);
        Assert.Equal("error", payload.GetProperty("completionReason").GetString());
    }
}
