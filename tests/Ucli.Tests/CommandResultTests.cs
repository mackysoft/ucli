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
                CommandResult.NotImplemented(UcliCommandNames.Status),
                UcliCommandNames.Status,
                (int)CliExitCode.ToolError,
                "COMMAND_NOT_IMPLEMENTED",
                $"Command '{UcliCommandNames.Status}' is not implemented yet."
            },
            {
                CommandResult.InvalidArgument(UcliCommandNames.Status, UnknownOptionMessage),
                UcliCommandNames.Status,
                (int)CliExitCode.InvalidArgument,
                "INVALID_ARGUMENT",
                UnknownOptionMessage
            },
            {
                CommandResult.Canceled(UcliCommandNames.Status, CanceledMessage),
                UcliCommandNames.Status,
                (int)CliExitCode.ToolError,
                "CANCELED",
                CanceledMessage
            },
            {
                CommandResult.Timeout(UcliCommandNames.Status, TimeoutMessage),
                UcliCommandNames.Status,
                (int)CliExitCode.ToolError,
                "IPC_TIMEOUT",
                TimeoutMessage
            },
            {
                CommandResult.InternalError(UcliCommandNames.Status, UnhandledExceptionMessage),
                UcliCommandNames.Status,
                (int)CliExitCode.ToolError,
                "INTERNAL_ERROR",
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
            expectedStatus: "ok",
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
            expectedCommand: UcliCommandNames.Root,
            expectedStatus: "ok",
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
            expectedStatus: "error",
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
            expectedCommand: UcliCommandNames.Root,
            expectedStatus: "error",
            expectedExitCode: (int)CliExitCode.ToolError,
            expectedMessage: expectedMessage);
        AssertSingleError(
            result,
            expectedCode: "INTERNAL_ERROR",
            expectedMessage: expectedMessage);
    }

    private static void AssertCommonContract (
        CommandResult result,
        string expectedCommand,
        string expectedStatus,
        int expectedExitCode,
        string expectedMessage)
    {
        Assert.Equal(1, result.ProtocolVersion);
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