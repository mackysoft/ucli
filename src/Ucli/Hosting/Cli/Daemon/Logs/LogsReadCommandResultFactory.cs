using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Creates public command results for <c>logs * read</c>. </summary>
internal static class LogsReadCommandResultFactory
{
    private const string StartDaemonOrCheckProjectPathActionRequired = "startDaemonOrCheckProjectPath";

    /// <summary> Creates one final command result from the logs-read service result. </summary>
    public static CommandResult Create (
        string commandName,
        LogsReadServiceResult serviceResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(serviceResult);

        var payload = new LogsReadCommandPayload(
            serviceResult.Count,
            serviceResult.NextCursor,
            serviceResult.CompletionReason,
            ResolveActionRequired(serviceResult));
        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(commandName, "Log read completed.", payload);
        }

        var failure = CreateFailure(serviceResult);
        return CommandFailureProjector.Create(commandName, failure.Message, payload, [failure]);
    }

    private static ApplicationFailure CreateFailure (LogsReadServiceResult serviceResult)
    {
        var error = serviceResult.Error ?? throw new ArgumentException("Failed logs read result must contain an error.", nameof(serviceResult));
        if (serviceResult.CompletionReason == LogsReadCompletionReason.Canceled)
        {
            return ApplicationFailure.Canceled(error.Message, ExecutionErrorCodes.Canceled);
        }

        return ApplicationFailure.FromExecutionError(error);
    }

    private static string? ResolveActionRequired (LogsReadServiceResult serviceResult)
    {
        return serviceResult.Error?.Code == DaemonErrorCodes.DaemonSessionNotAvailable
            ? StartDaemonOrCheckProjectPathActionRequired
            : null;
    }
}
