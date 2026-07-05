using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;

namespace MackySoft.Tests;

internal static class PlayCommandAssert
{
    public static void EnterSucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingPlayEnterService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds)
    {
        SucceededWithDispatchedInput(
            result,
            service.Invocations,
            expectedCancellationToken,
            new PlayEnterCommandInput(
                expectedProjectPath,
                expectedTimeoutMilliseconds));
    }

    public static void ExitSucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingPlayExitService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds)
    {
        SucceededWithDispatchedInput(
            result,
            service.Invocations,
            expectedCancellationToken,
            new PlayExitCommandInput(
                expectedProjectPath,
                expectedTimeoutMilliseconds));
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
                expectedProjectPath,
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
    }

    public static void InvalidTimeoutRejectedBeforeExitExecution (
        CommandExecutionResult result,
        RecordingPlayExitService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.PlayExit);
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
}
