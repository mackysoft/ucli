using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class CommandResultFactoryTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)ExecutionErrorKind.InvalidArgument, "INVALID_ARGUMENT", (int)CliExitCode.InvalidArgument)]
    [InlineData((int)ExecutionErrorKind.Timeout, "IPC_TIMEOUT", (int)CliExitCode.ToolError)]
    [InlineData((int)ExecutionErrorKind.InternalError, "INTERNAL_ERROR", (int)CliExitCode.ToolError)]
    public void FromExecutionError_MapsErrorKindToCommandResult (
        int errorKind,
        string expectedErrorCode,
        int expectedExitCode)
    {
        const string command = "test";
        const string message = "error";
        var error = new ExecutionError((ExecutionErrorKind)errorKind, message);

        var result = CommandResultFactory.FromExecutionError(command, error);

        Assert.Equal(command, result.Command);
        Assert.Equal("error", result.Status);
        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(expectedErrorCode, result.Errors[0].Code);
        Assert.Equal(message, result.Errors[0].Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromExecutionError_WithCustomCode_PreservesCodeAndKindExitCode ()
    {
        const string command = "test";
        const string message = "project path does not exist";
        var error = ExecutionError.InvalidArgument(
            message,
            ProjectContextErrorCodes.ProjectPathNotFound);

        var result = CommandResultFactory.FromExecutionError(command, error);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Equal(ProjectContextErrorCodes.ProjectPathNotFound, Assert.Single(result.Errors).Code);
    }
}
