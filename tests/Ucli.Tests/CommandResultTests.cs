using System.Text.Json;
using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests;

public sealed class CommandResultTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    private const string UnhandledExceptionMessage = "Unhandled exception.";

    private const string CanceledMessage = "Command execution was canceled.";

    private const string TimeoutMessage = "IPC request timed out.";

    public static TheoryData<object, string, int, string, string> ErrorCaseData => new()
        {
            {
                CommandResult.NotImplemented(StatusCommand.CommandName),
                StatusCommand.CommandName,
                (int)CliExitCode.ToolError,
                ErrorCodes.CommandNotImplemented,
                $"Command '{StatusCommand.CommandName}' is not implemented yet."
            },
            {
                CommandResult.InvalidArgument(StatusCommand.CommandName, UnknownOptionMessage),
                StatusCommand.CommandName,
                (int)CliExitCode.InvalidArgument,
                ErrorCodes.InvalidArgument,
                UnknownOptionMessage
            },
            {
                CommandResult.Canceled(StatusCommand.CommandName, CanceledMessage),
                StatusCommand.CommandName,
                (int)CliExitCode.ToolError,
                ErrorCodes.Canceled,
                CanceledMessage
            },
            {
                CommandResult.Timeout(StatusCommand.CommandName, TimeoutMessage),
                StatusCommand.CommandName,
                (int)CliExitCode.ToolError,
                ErrorCodes.IpcTimeout,
                TimeoutMessage
            },
            {
                CommandResult.InternalError(StatusCommand.CommandName, UnhandledExceptionMessage),
                StatusCommand.CommandName,
                (int)CliExitCode.ToolError,
                ErrorCodes.InternalError,
                UnhandledExceptionMessage
            },
        };

    [Fact]
    [Trait("Size", "Small")]
    public void Success_CreatesContractCompliantResult ()
    {
        const string message = "Initialized.";
        var payload = new { initialized = true };
        var result = CommandResult.Success(InitCommand.CommandName, message, payload);

        AssertCommonContract(
            result,
            expectedCommand: InitCommand.CommandName,
            expectedStatus: CliProtocol.StatusOk,
            expectedExitCode: (int)CliExitCode.Success,
            expectedMessage: message);
        Assert.Equal(JsonValueKind.Object, JsonSerializer.SerializeToElement(result.Payload).ValueKind);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData("   ")]
    public void Success_NormalizesWhitespaceCommandAndMessage (string whitespaceValue)
    {
        var result = CommandResult.Success(whitespaceValue, whitespaceValue);

        AssertCommonContract(
            result,
            expectedCommand: CliProtocol.RootCommand,
            expectedStatus: CliProtocol.StatusOk,
            expectedExitCode: (int)CliExitCode.Success,
            expectedMessage: "An unknown error occurred.");
        Assert.Equal(JsonValueKind.Object, JsonSerializer.SerializeToElement(result.Payload).ValueKind);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ErrorCaseData))]
    public void ErrorFactory_CreatesContractCompliantResult (
        object actualResult,
        string expectedCommand,
        int expectedExitCode,
        string expectedErrorCode,
        string expectedMessage)
    {
        var result = Assert.IsType<CommandResult>(actualResult);

        AssertCommonContract(
            result,
            expectedCommand: expectedCommand,
            expectedStatus: CliProtocol.StatusError,
            expectedExitCode: expectedExitCode,
            expectedMessage: expectedMessage);
        AssertSingleError(
            result,
            expectedCode: expectedErrorCode,
            expectedMessage: expectedMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    public void InternalError_NormalizesWhitespaceCommandAndMessage (string whitespaceValue)
    {
        const string expectedMessage = "An unknown error occurred.";
        var result = CommandResult.InternalError(whitespaceValue, whitespaceValue);

        AssertCommonContract(
            result,
            expectedCommand: CliProtocol.RootCommand,
            expectedStatus: CliProtocol.StatusError,
            expectedExitCode: (int)CliExitCode.ToolError,
            expectedMessage: expectedMessage);
        AssertSingleError(
            result,
            expectedCode: ErrorCodes.InternalError,
            expectedMessage: expectedMessage);
    }

    private static void AssertCommonContract (
        CommandResult result,
        string expectedCommand,
        string expectedStatus,
        int expectedExitCode,
        string expectedMessage)
    {
        Assert.Equal(CliProtocol.CurrentVersion, result.ProtocolVersion);
        Assert.Equal(expectedCommand, result.Command);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.Equal(expectedMessage, result.Message);
    }

    private static void AssertSingleError (
        CommandResult result,
        string expectedCode,
        string expectedMessage)
    {
        Assert.Equal(JsonValueKind.Object, JsonSerializer.SerializeToElement(result.Payload).ValueKind);
        Assert.Single(result.Errors);
        Assert.Equal(expectedCode, result.Errors[0].Code);
        Assert.Equal(expectedMessage, result.Errors[0].Message);
        Assert.Null(result.Errors[0].OpId);
    }
}