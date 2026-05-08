using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates shared failure envelopes for request command results. </summary>
internal static class RequestCommandFailureResultFactory
{
    /// <summary> Creates one failed command result from a verified application result state. </summary>
    public static CommandResult Create (
        string command,
        string message,
        object payload,
        IReadOnlyList<ApplicationFailure> errors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(errors);

        return CommandFailureProjector.Create(command, message, payload, errors);
    }
}
