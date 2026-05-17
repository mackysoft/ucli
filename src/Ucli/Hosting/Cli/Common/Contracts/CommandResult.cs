using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Represents the JSON contract payload emitted by every CLI command execution. </summary>
/// <param name="ProtocolVersion"> The protocol version of the emitted JSON payload. </param>
/// <param name="Command"> The normalized command name associated with this result. </param>
/// <param name="Status"> The execution status string defined by <see cref="IpcProtocol" />. </param>
/// <param name="ExitCode"> The process exit code associated with this result. </param>
/// <param name="Message"> The user-facing message that explains the execution outcome. </param>
/// <param name="Payload"> The JSON-serializable payload object for additional command output. </param>
/// <param name="Errors"> The machine-readable error list. Empty when <paramref name="Status" /> is <c>ok</c>. </param>
internal sealed record CommandResult (
    int ProtocolVersion,
    string Command,
    string Status,
    int ExitCode,
    string Message,
    object Payload,
    IReadOnlyList<CommandError> Errors)
{
    private static readonly object EmptyPayload = new();

    private static readonly IReadOnlyList<CommandError> EmptyErrors = Array.Empty<CommandError>();

    /// <summary> Creates a successful command result. </summary>
    /// <param name="command"> The command name written to the result. <see langword="null" />, empty, and whitespace values are normalized to <see cref="UcliCommandNames.Root" />. </param>
    /// <param name="message"> The success message written to the result. <see langword="null" />, empty, and whitespace values are replaced by a fallback message. </param>
    /// <param name="payload"> The command payload. When <see langword="null" />, an empty payload object is used. </param>
    /// <returns> A command result with <c>ok</c> status and the success exit code. </returns>
    public static CommandResult Success (string command, string message, object? payload = null)
    {
        var normalizedCommand = NormalizeCommand(command);
        var normalizedMessage = NormalizeMessage(message);
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: normalizedCommand,
            Status: IpcProtocol.StatusOk,
            ExitCode: (int)CliExitCode.Success,
            Message: normalizedMessage,
            Payload: payload ?? EmptyPayload,
            Errors: EmptyErrors);
    }

    /// <summary> Creates a placeholder error result for a command that is not implemented yet. </summary>
    /// <param name="command"> The command name written to the result. <see langword="null" />, empty, and whitespace values are normalized to <see cref="UcliCommandNames.Root" />. </param>
    /// <param name="message"> The optional custom message. When <see langword="null" />, a default not-implemented message is generated. </param>
    /// <returns> A command result with <c>error</c> status and the <c>COMMAND_NOT_IMPLEMENTED</c> error code. </returns>
    public static CommandResult NotImplemented (string command, string? message = null)
    {
        var normalizedCommand = NormalizeCommand(command);
        var normalizedMessage = message ?? $"Command '{normalizedCommand}' is not implemented yet.";
        return CreateError(
            command: normalizedCommand,
            message: normalizedMessage,
            exitCode: CliExitCode.ToolError,
            errorCode: UcliCoreErrorCodes.CommandNotImplemented);
    }

    /// <summary> Creates an error result for invalid command arguments. </summary>
    /// <param name="command"> The command name written to the result. <see langword="null" />, empty, and whitespace values are normalized to <see cref="UcliCommandNames.Root" />. </param>
    /// <param name="message"> The argument validation message written to the result. <see langword="null" />, empty, and whitespace values are replaced by a fallback message. </param>
    /// <param name="errorCode"> The optional machine-readable error code. When omitted, <c>INVALID_ARGUMENT</c> is used. </param>
    /// <returns> A command result with <c>error</c> status and the invalid-argument exit code. </returns>
    public static CommandResult InvalidArgument (
        string command,
        string message,
        UcliCodeValue? errorCode = null)
    {
        return CreateError(
            command: command,
            message: message,
            exitCode: CliExitCode.InvalidArgument,
            errorCode: errorCode.HasValue && errorCode.Value.IsValid
                ? errorCode.Value
                : UcliCoreErrorCodes.InvalidArgument);
    }

    /// <summary> Creates an error result for command cancellation. </summary>
    /// <param name="command"> The command name written to the result. <see langword="null" />, empty, and whitespace values are normalized to <see cref="UcliCommandNames.Root" />. </param>
    /// <param name="message"> The cancellation message written to the result. <see langword="null" />, empty, and whitespace values are replaced by a fallback message. </param>
    /// <returns> A command result with <c>error</c> status and the tool-error exit code. </returns>
    public static CommandResult Canceled (string command, string message)
    {
        return CreateError(
            command: command,
            message: message,
            exitCode: CliExitCode.ToolError,
            errorCode: ExecutionErrorCodes.Canceled);
    }

    /// <summary> Creates an error result for infrastructure timeouts. </summary>
    /// <param name="command"> The command name written to the result. <see langword="null" />, empty, and whitespace values are normalized to <see cref="UcliCommandNames.Root" />. </param>
    /// <param name="message"> The timeout message written to the result. <see langword="null" />, empty, and whitespace values are replaced by a fallback message. </param>
    /// <param name="errorCode"> The optional machine-readable error code. When omitted, <c>IPC_TIMEOUT</c> is used. </param>
    /// <returns> A command result with <c>error</c> status and the tool-error exit code. </returns>
    public static CommandResult Timeout (
        string command,
        string message,
        UcliCodeValue? errorCode = null)
    {
        return CreateError(
            command: command,
            message: message,
            exitCode: CliExitCode.ToolError,
            errorCode: errorCode.HasValue && errorCode.Value.IsValid
                ? errorCode.Value
                : ExecutionErrorCodes.IpcTimeout);
    }

    /// <summary> Creates an error result for unexpected runtime failures. </summary>
    /// <param name="command"> The command name written to the result. <see langword="null" />, empty, and whitespace values are normalized to <see cref="UcliCommandNames.Root" />. </param>
    /// <param name="message"> The failure message written to the result. <see langword="null" />, empty, and whitespace values are replaced by a fallback message. </param>
    /// <param name="errorCode"> The optional machine-readable error code. When omitted, <c>INTERNAL_ERROR</c> is used. </param>
    /// <returns> A command result with <c>error</c> status and the tool-error exit code. </returns>
    public static CommandResult InternalError (
        string command,
        string message,
        UcliCodeValue? errorCode = null)
    {
        return CreateError(
            command: command,
            message: message,
            exitCode: CliExitCode.ToolError,
            errorCode: errorCode.HasValue && errorCode.Value.IsValid
                ? errorCode.Value
                : UcliCoreErrorCodes.InternalError);
    }

    /// <summary> Creates a normalized error result with a single error entry. </summary>
    /// <param name="command"> The command name written to the result. </param>
    /// <param name="message"> The error message written to the result. </param>
    /// <param name="exitCode"> The exit code associated with the error result. </param>
    /// <param name="errorCode"> The machine-readable error code added to the error list. </param>
    /// <returns> A normalized command result with <c>error</c> status. </returns>
    private static CommandResult CreateError (
        string command,
        string message,
        CliExitCode exitCode,
        UcliCodeValue errorCode)
    {
        var normalizedCommand = NormalizeCommand(command);
        var normalizedMessage = NormalizeMessage(message);

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: normalizedCommand,
            Status: IpcProtocol.StatusError,
            ExitCode: (int)exitCode,
            Message: normalizedMessage,
            Payload: EmptyPayload,
            Errors:
            [
                new CommandError(errorCode, normalizedMessage, null),
            ]);
    }

    /// <summary> Normalizes the command name used in command results. </summary>
    /// <param name="command"> The command name to normalize. </param>
    /// <returns> The input command name, or <see cref="UcliCommandNames.Root" /> when the input is <see langword="null" />, empty, or whitespace. </returns>
    private static string NormalizeCommand (string command)
    {
        return string.IsNullOrWhiteSpace(command) ? UcliCommandNames.Root : command;
    }

    /// <summary> Normalizes the message value used in command results. </summary>
    /// <param name="message"> The message to normalize. </param>
    /// <returns> The input message, or a fallback error message when the input is <see langword="null" />, empty, or whitespace. </returns>
    private static string NormalizeMessage (string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "An unknown error occurred." : message;
    }
}
