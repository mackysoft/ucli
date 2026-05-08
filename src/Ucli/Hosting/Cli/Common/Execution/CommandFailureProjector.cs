using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Projects classified application failures to the public CLI command-result contract. </summary>
internal static class CommandFailureProjector
{
    private static readonly object EmptyPayload = new();

    /// <summary> Creates one failed command result from classified failures. </summary>
    public static CommandResult Create (
        string command,
        string message,
        object? payload,
        IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
        {
            throw new ArgumentException("Failure collection must not be empty.", nameof(failures));
        }

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: command,
            Status: IpcProtocol.StatusError,
            ExitCode: ApplicationOutcomeCliExitCodeMapper.ToExitCode(ApplicationFailureOutcomeResolver.Resolve(failures)),
            Message: message,
            Payload: payload ?? EmptyPayload,
            Errors: CreateErrors(failures));
    }

    /// <summary> Creates one failed command result from one classified failure. </summary>
    public static CommandResult Create (
        string command,
        ApplicationFailure failure,
        object? payload = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return Create(command, failure.Message, payload, [failure]);
    }

    /// <summary> Converts classified application failures to CLI error entries. </summary>
    public static IReadOnlyList<CommandError> CreateErrors (IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var errors = new CommandError[failures.Count];
        for (var i = 0; i < failures.Count; i++)
        {
            var failure = failures[i];
            if (failure == null)
            {
                throw new ArgumentException("Failure collection must not contain null entries.", nameof(failures));
            }

            errors[i] = new CommandError(failure.Code, failure.Message, failure.OpId);
        }

        return errors;
    }
}
