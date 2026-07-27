using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Creates public command results for <c>logs * read</c>. </summary>
internal static class LogsReadCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>logs * read</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(LogsReadCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>logs * read</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<LogsReadCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<LogsReadCommandPayload>();
    }

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
        return CommandFailureProjector.Create(
            commandName,
            failure.Message,
            CommandErrorPayload.Detailed(payload),
            [failure]);
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

    private static LogsReadActionRequired? ResolveActionRequired (LogsReadServiceResult serviceResult)
    {
        return serviceResult.Error?.Code == DaemonErrorCodes.DaemonSessionNotAvailable
            ? LogsReadActionRequired.StartDaemonOrCheckProjectPath
            : null;
    }
}
