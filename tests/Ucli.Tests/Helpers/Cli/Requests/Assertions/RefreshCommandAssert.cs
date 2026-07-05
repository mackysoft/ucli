using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Tests;

internal static class RefreshCommandAssert
{
    public static void SucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingRefreshService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        bool expectedFailFast)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(
            new RefreshCommandInput(
                expectedProjectPath,
                expectedMode,
                expectedTimeoutMilliseconds,
                expectedFailFast),
            invocation.Input);
    }

    public static void SucceededWithPayload (
        CommandExecutionResult result,
        string expectedRequestId)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI refresh completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", expectedRequestId)
                .HasProperty("project", project => project
                    .HasString("projectPath", ProjectIdentityInfoTestFactory.DefaultProjectPath)
                    .HasString("projectFingerprint", ProjectIdentityInfoTestFactory.ProjectFingerprint)
                    .HasString("unityVersion", ProjectIdentityInfoTestFactory.UnityVersion))
                .HasArrayLength("opResults", 1)
                .HasProperty("opResults", 0, op => op
                    .HasString("opId", "refresh")
                    .HasString("op", UcliPrimitiveOperationNames.ProjectRefresh)
                    .HasString("phase", "call")
                    .HasBoolean("applied", true)
                    .HasBoolean("changed", true)
                    .HasArrayLength("touched", 1)));
    }

    public static void InvalidArgumentReturnedWithoutRefreshExecution (
        CommandExecutionResult result,
        RecordingRefreshService service)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh);
        HasRefreshFailurePayload(outputJson.RootElement);
    }

    private static void HasRefreshFailurePayload (JsonElement rootElement)
    {
        var payload = rootElement.GetProperty("payload");
        var requestId = payload.GetProperty("requestId").GetString();

        Assert.True(Guid.TryParseExact(requestId, "D", out _));
        JsonAssert.For(payload)
            .HasArrayLength("opResults", 0);
    }
}
