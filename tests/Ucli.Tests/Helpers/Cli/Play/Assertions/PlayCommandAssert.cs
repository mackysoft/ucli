using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Tests;

internal static class PlayCommandAssert
{
    public static void EnterSucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingLifecycleExecutionStartInvocationFactory invocationFactory,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds)
    {
        SucceededWithPlayStart(
            result,
            invocationFactory.PlayEnterRequests,
            expectedCancellationToken,
            expectedProjectPath,
            expectedTimeoutMilliseconds);
    }

    public static void ExitSucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingLifecycleExecutionStartInvocationFactory invocationFactory,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds)
    {
        SucceededWithPlayStart(
            result,
            invocationFactory.PlayExitRequests,
            expectedCancellationToken,
            expectedProjectPath,
            expectedTimeoutMilliseconds);
    }

    public static void StatusSucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingPlayStatusService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds)
    {
        SucceededWithDispatchedInput(
            result,
            service.Invocations,
            expectedCancellationToken,
            new PlayStatusCommandInput(
                AbsolutePath.Parse(expectedProjectPath),
                expectedTimeoutMilliseconds));
    }

    public static void InvalidTimeoutRejectedBeforeEnterExecution (
        CommandExecutionResult result,
        RecordingPlayEnterService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.PlayEnter);
        HasEmptyTransitionErrorPayload(result);
    }

    public static void InvalidTimeoutRejectedBeforeExitExecution (
        CommandExecutionResult result,
        RecordingPlayExitService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.PlayExit);
        HasEmptyTransitionErrorPayload(result);
    }

    public static void InvalidTimeoutRejectedBeforeStatusExecution (
        CommandExecutionResult result,
        RecordingPlayStatusService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.PlayStatus);
    }

    private static void SucceededWithDispatchedInput<TInput> (
        CommandExecutionResult result,
        IReadOnlyList<CommandServiceInvocation<TInput>> invocations,
        CancellationToken expectedCancellationToken,
        TInput expectedInput)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(expectedInput, invocation.Input);
    }

    private static void SucceededWithPlayStart (
        CommandExecutionResult result,
        IReadOnlyList<RecordingLifecycleExecutionStartInvocationFactory.PlayStartRequest> requests,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var request = Assert.Single(requests);
        Assert.Equal(expectedCancellationToken, request.CancellationToken);
        ProjectPathDispatchAssert.EqualNormalized(expectedProjectPath, request.ProjectPath);
        Assert.Equal(UnityExecutionMode.Daemon, request.RequestedMode);
        Assert.Equal(expectedTimeoutMilliseconds, request.TimeoutMilliseconds);
    }

    private static void HasEmptyTransitionErrorPayload (
        CommandExecutionResult result)
    {
        using var outputJson =
            JsonAssert.ParseMultilineObject(result.StdOut);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "empty");
        Assert.Equal(
            new[] { "payloadKind" },
            payload.EnumerateObject()
                .Select(static property => property.Name));
    }
}
