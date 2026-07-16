using System.Text.Json;

namespace MackySoft.Ucli.Tests;

public sealed class CommandResultTests
{
    private const string UnknownOptionMessage = "Argument '--unknown' is not recognized.";

    private const string UnhandledExceptionMessage = "Unhandled exception.";

    private const string CanceledMessage = "Command execution was canceled.";

    private const string TimeoutMessage = "IPC request timed out.";

    private static readonly ErrorCase[] ErrorCases =
    [
        new(
            static () => CommandResult.NotImplemented(UcliCommandNames.Status),
            UcliCommandNames.Status,
            (int)CliExitCode.ToolError,
            "COMMAND_NOT_IMPLEMENTED",
            $"Command '{UcliCommandNames.Status}' is not implemented yet."),
        new(
            static () => CommandResult.InvalidArgument(UcliCommandNames.Status, UnknownOptionMessage),
            UcliCommandNames.Status,
            (int)CliExitCode.InvalidArgument,
            "INVALID_ARGUMENT",
            UnknownOptionMessage),
        new(
            static () => CommandResult.Canceled(UcliCommandNames.Status, CanceledMessage),
            UcliCommandNames.Status,
            (int)CliExitCode.ToolError,
            "CANCELED",
            CanceledMessage),
        new(
            static () => CommandResult.Timeout(UcliCommandNames.Status, TimeoutMessage),
            UcliCommandNames.Status,
            (int)CliExitCode.ToolError,
            "IPC_TIMEOUT",
            TimeoutMessage),
        new(
            static () => CommandResult.InternalError(UcliCommandNames.Status, UnhandledExceptionMessage),
            UcliCommandNames.Status,
            (int)CliExitCode.ToolError,
            "INTERNAL_ERROR",
            UnhandledExceptionMessage),
    ];

    private static readonly string[] WhitespaceValues =
    [
        string.Empty,
        " ",
        "   ",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Success_CreatesContractCompliantResult ()
    {
        const string message = "Initialized.";
        var payload = new { initialized = true };
        var result = CommandResult.Success(UcliCommandNames.Init, message, payload);

        AssertCommonContract(
            result,
            expectedCommand: UcliCommandNames.Init,
            expectedStatus: CommandResultStatus.Ok,
            expectedExitCode: (int)CliExitCode.Success,
            expectedMessage: message);
        Assert.Equal(JsonValueKind.Object, JsonSerializer.SerializeToElement(result.Payload).ValueKind);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Success_NormalizesWhitespaceCommandAndMessage ()
    {
        foreach (var whitespaceValue in WhitespaceValues)
        {
            var result = CommandResult.Success(whitespaceValue, whitespaceValue);

            AssertCommonContract(
                result,
                expectedCommand: UcliCommandNames.Root,
                expectedStatus: CommandResultStatus.Ok,
                expectedExitCode: (int)CliExitCode.Success,
                expectedMessage: "An unknown error occurred.");
            Assert.Equal(JsonValueKind.Object, JsonSerializer.SerializeToElement(result.Payload).ValueKind);
            Assert.Empty(result.Errors);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ErrorFactory_CreatesContractCompliantResult ()
    {
        foreach (var testCase in ErrorCases)
        {
            var result = testCase.CreateResult();

            AssertCommonContract(
                result,
                expectedCommand: testCase.ExpectedCommand,
                expectedStatus: CommandResultStatus.Error,
                expectedExitCode: testCase.ExpectedExitCode,
                expectedMessage: testCase.ExpectedMessage);
            AssertSingleError(
                result,
                expectedCode: testCase.ExpectedErrorCode,
                expectedMessage: testCase.ExpectedMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InternalError_NormalizesWhitespaceCommandAndMessage ()
    {
        const string expectedMessage = "An unknown error occurred.";
        foreach (var whitespaceValue in WhitespaceValues)
        {
            var result = CommandResult.InternalError(whitespaceValue, whitespaceValue);

            AssertCommonContract(
                result,
                expectedCommand: UcliCommandNames.Root,
                expectedStatus: CommandResultStatus.Error,
                expectedExitCode: (int)CliExitCode.ToolError,
                expectedMessage: expectedMessage);
            AssertSingleError(
                result,
                expectedCode: "INTERNAL_ERROR",
                expectedMessage: expectedMessage);
        }
    }

    private static void AssertCommonContract (
        CommandResult result,
        string expectedCommand,
        CommandResultStatus expectedStatus,
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
        Assert.Equal(expectedCode, result.Errors[0].Code.Value);
        Assert.Equal(expectedMessage, result.Errors[0].Message);
        Assert.Null(result.Errors[0].OpId);
    }

    private sealed record ErrorCase (
        Func<CommandResult> CreateResult,
        string ExpectedCommand,
        int ExpectedExitCode,
        string ExpectedErrorCode,
        string ExpectedMessage);
}
