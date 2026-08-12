using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Tests;

namespace MackySoft.Tests;

internal static class CompileCommandAssert
{
    public static void SucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingLifecycleExecutionStartInvocationFactory invocationFactory,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(invocationFactory.CompileRequests);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        ProjectPathDispatchAssert.EqualNormalized(expectedProjectPath, invocation.ProjectPath);
        Assert.Equal(expectedMode, invocation.RequestedMode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.TimeoutMilliseconds);
    }

    public static void SucceededWithOnlyFinalOutputAndGolden (
        CommandExecutionResult result,
        RecordingCompileService service,
        JsonGoldenFileNormalization normalization)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(string.Empty, result.StdErr);
        Assert.Single(service.Invocations);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("compile", "pass-no-reload.json"),
            result.StdOut,
            normalization);
    }

    public static void InvalidArgumentReturnedWithoutCompileExecution (
        CommandExecutionResult result,
        RecordingCompileService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailureWithEmptyStandardError(
            result,
            service.Invocations,
            UcliCommandNames.Compile);
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
