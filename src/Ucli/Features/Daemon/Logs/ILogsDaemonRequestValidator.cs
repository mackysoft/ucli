using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Logs;

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