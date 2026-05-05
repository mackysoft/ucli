using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;

/// <summary> Validates raw <c>logs daemon</c> request values. </summary>
internal sealed class LogsDaemonRequestValidator : ILogsDaemonRequestValidator
{
    /// <inheritdoc />
    public bool TryValidate (
        LogsDaemonServiceRequest request,
        out IpcDaemonLogsReadRequest? query,
        out LogsStreamRuntimeOptions? streamOptions,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        query = null;
        streamOptions = null;

        var ipcRequest = new IpcDaemonLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: request.Level,
            Query: request.Query,
            QueryTarget: request.QueryTarget,
            Category: request.Category);
        if (!IpcDaemonLogsReadRequestNormalizer.TryNormalize(
                ipcRequest,
                out var normalizedQuery,
                out _,
                out var untilTimestamp,
                out var commonValidationErrorMessage))
        {
            error = ExecutionError.InvalidArgument(commonValidationErrorMessage!);
            return false;
        }

        if (!LogsStreamRuntimeOptionsValidator.TryValidate(
                request.Stream,
                request.PollIntervalMilliseconds,
                request.IdleTimeoutMilliseconds,
                untilTimestamp,
                out var validatedStreamOptions,
                out error))
        {
            return false;
        }

        query = normalizedQuery;
        streamOptions = validatedStreamOptions;
        return true;
    }
}
