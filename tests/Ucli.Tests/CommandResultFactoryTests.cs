using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Tests;

public sealed class CommandResultFactoryTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)ExecutionErrorKind.InvalidArgument, ErrorCodes.InvalidArgument, (int)CliExitCode.InvalidArgument)]
    [InlineData((int)ExecutionErrorKind.Timeout, ErrorCodes.IpcTimeout, (int)CliExitCode.ToolError)]
    [InlineData((int)ExecutionErrorKind.InternalError, ErrorCodes.InternalError, (int)CliExitCode.ToolError)]
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
        Assert.Equal(CliProtocol.StatusError, result.Status);
        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(expectedErrorCode, result.Errors[0].Code);
        Assert.Equal(message, result.Errors[0].Message);
    }
}