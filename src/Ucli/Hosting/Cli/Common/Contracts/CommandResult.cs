using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Represents the JSON contract payload emitted by every CLI command execution. </summary>
internal readonly record struct CommandResult
{
    private static readonly IReadOnlyList<CommandError> EmptyErrors = Array.Empty<CommandError>();

    /// <summary> Initializes one command result with a non-null object payload root. </summary>
    public CommandResult (
        int ProtocolVersion,
        string Command,
        CommandResultStatus Status,
        int ExitCode,
        string Message,
        object Payload,
        IReadOnlyList<CommandError> Errors)
    {
        this.ProtocolVersion = ProtocolVersion;
        this.Command = Command ?? throw new ArgumentNullException(nameof(Command));
        this.Status = Status;
        this.ExitCode = ExitCode;
        this.Message = Message ?? throw new ArgumentNullException(nameof(Message));
        this.Payload = UcliNonNullJsonObject.Wrap(
            Payload ?? throw new ArgumentNullException(nameof(Payload)),
            CliOutputJsonSerializerOptions.Default);
        this.Errors = Errors ?? throw new ArgumentNullException(nameof(Errors));
    }

    /// <summary> Gets the protocol version of the emitted JSON payload. </summary>
    public int ProtocolVersion { get; }

    /// <summary> Gets the normalized command name associated with this result. </summary>
    public string Command { get; }

    /// <summary> Gets the execution status. </summary>
    public CommandResultStatus Status { get; }

    /// <summary> Gets the process exit code associated with this result. </summary>
    public int ExitCode { get; }

    /// <summary> Gets the user-facing message that explains the execution outcome. </summary>
    public string Message { get; }

    /// <summary> Gets the actual non-null JSON object payload serialized by the CLI boundary. </summary>
    public IUcliNonNullJsonObject Payload { get; }

    /// <summary> Gets the machine-readable error list. </summary>
    public IReadOnlyList<CommandError> Errors { get; }

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
            Status: CommandResultStatus.Ok,
            ExitCode: (int)CliExitCode.Success,
            Message: normalizedMessage,
            Payload: payload ?? EmptyCommandPayload.Instance,
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
        UcliCode? errorCode = null,
        object? payload = null)
    {
        return CreateError(
            command: command,
            message: message,
            exitCode: CliExitCode.InvalidArgument,
            errorCode: errorCode ?? UcliCoreErrorCodes.InvalidArgument,
            payload: payload);
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
        UcliCode? errorCode = null)
    {
        return CreateError(
            command: command,
            message: message,
            exitCode: CliExitCode.ToolError,
            errorCode: errorCode ?? ExecutionErrorCodes.IpcTimeout);
    }

    /// <summary> Creates an error result for unexpected runtime failures. </summary>
    /// <param name="command"> The command name written to the result. <see langword="null" />, empty, and whitespace values are normalized to <see cref="UcliCommandNames.Root" />. </param>
    /// <param name="message"> The failure message written to the result. <see langword="null" />, empty, and whitespace values are replaced by a fallback message. </param>
    /// <param name="errorCode"> The optional machine-readable error code. When omitted, <c>INTERNAL_ERROR</c> is used. </param>
    /// <returns> A command result with <c>error</c> status and the tool-error exit code. </returns>
    public static CommandResult InternalError (
        string command,
        string message,
        UcliCode? errorCode = null)
    {
        return CreateError(
            command: command,
            message: message,
            exitCode: CliExitCode.ToolError,
            errorCode: errorCode ?? UcliCoreErrorCodes.InternalError);
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
        UcliCode errorCode,
        object? payload = null)
    {
        var normalizedCommand = NormalizeCommand(command);
        var normalizedMessage = NormalizeMessage(message);

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: normalizedCommand,
            Status: CommandResultStatus.Error,
            ExitCode: (int)exitCode,
            Message: normalizedMessage,
            Payload: payload ?? EmptyCommandPayload.Instance,
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
