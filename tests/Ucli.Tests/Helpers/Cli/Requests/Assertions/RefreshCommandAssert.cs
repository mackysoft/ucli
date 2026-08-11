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
        Assert.NotEqual(Guid.Empty, Assert.Single(service.RequestIds));
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        ProjectPathDispatchAssert.EqualNormalized(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedFailFast, invocation.Input.FailFast);
    }

    public static void SucceededWithPayload (
        CommandExecutionResult result,
        string expectedRequestId,
        string expectedExecutionId)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var payloadElement = outputJson.RootElement.GetProperty("payload");
        Assert.False(payloadElement.TryGetProperty("opResults", out _));
        Assert.False(payloadElement.TryGetProperty("planToken", out _));
        Assert.False(payloadElement.TryGetProperty("contractViolations", out _));
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI refresh completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", expectedRequestId)
                .HasProperty("project", project => project
                    .HasString("projectPath", ProjectIdentityInfoTestFactory.DefaultProjectPath)
                    .HasString("projectFingerprint", ProjectIdentityInfoTestFactory.ProjectFingerprint.ToString())
                    .HasString("unityVersion", ProjectIdentityInfoTestFactory.UnityVersion))
                .HasProperty("lifecycleExecutionRef", reference => reference
                    .HasString("lifecycle", "terminal")
                    .HasString("kind", "refresh")
                    .HasString("id", expectedExecutionId))
                .HasProperty("refresh", refresh => refresh
                    .HasInt32("domainReloadGenerationBefore", 1)
                    .HasInt32("domainReloadGenerationAfter", 2))
                .HasProperty("lifecycle", lifecycle => lifecycle
                    .HasProperty("state", state => state
                        .HasProperty("generations", generations =>
                            generations.HasInt32("domainReloadGeneration", 2)))));
    }

    public static void InvalidArgumentReturnedWithoutRefreshExecution (
        CommandExecutionResult result,
        RecordingRefreshService service)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);

        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh);
        HasRefreshFailurePayload(outputJson.RootElement);
    }

    private static void HasRefreshFailurePayload (JsonElement rootElement)
    {
        JsonAssert.For(rootElement.GetProperty("payload"))
            .HasString("payloadKind", "empty");
    }
}
