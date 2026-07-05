using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Tests;

namespace MackySoft.Tests;

internal static class CompileCommandAssert
{
    public static void SucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingCompileService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.NotNull(invocation.ProgressSink);
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
        RecordingCompileService service,
        bool expectEmptyStandardError)
    {
        if (expectEmptyStandardError)
        {
            CommandResultAssert.HasPreDispatchInvalidArgumentFailureWithEmptyStandardError(
                result,
                service.Invocations,
                UcliCommandNames.Compile);
        }
        else
        {
            CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
                result,
                service.Invocations,
                UcliCommandNames.Compile);
        }
    }
}
