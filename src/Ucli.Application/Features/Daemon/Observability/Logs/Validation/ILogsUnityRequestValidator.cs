using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;

/// <summary> Validates raw <c>logs unity</c> request values. </summary>
internal interface ILogsUnityRequestValidator
{
    /// <summary> Tries to validate one raw request and resolve normalized runtime values. </summary>
    /// <param name="request"> The raw request values. </param>
    /// <param name="query"> The normalized Unity-log IPC query when validation succeeds. </param>
    /// <param name="streamOptions"> The validated stream runtime options when validation succeeds. </param>
    /// <param name="error"> The invalid-argument error when validation fails. </param>
    /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
    bool TryValidate (
        LogsUnityServiceRequest request,
        out IpcUnityLogsReadRequest? query,
        out LogsStreamRuntimeOptions? streamOptions,
        out ExecutionError? error);
}
