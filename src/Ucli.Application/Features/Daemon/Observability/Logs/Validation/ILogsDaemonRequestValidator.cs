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
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;

/// <summary> Validates one <c>logs daemon</c> request and resolves derived runtime values. </summary>
internal interface ILogsDaemonRequestValidator
{
    /// <summary> Validates request values and resolves stream runtime options. </summary>
    /// <param name="request"> The command request values. </param>
    /// <param name="query"> The normalized daemon-log IPC query when validation succeeds. </param>
    /// <param name="streamOptions"> The validated stream runtime options when validation succeeds. </param>
    /// <param name="error"> Structured invalid-argument error when validation fails. </param>
    /// <returns> <see langword="true" /> when request is valid; otherwise <see langword="false" />. </returns>
    bool TryValidate (
        LogsDaemonServiceRequest request,
        out IpcDaemonLogsReadRequest? query,
        out LogsStreamRuntimeOptions? streamOptions,
        out ExecutionError? error);
}
