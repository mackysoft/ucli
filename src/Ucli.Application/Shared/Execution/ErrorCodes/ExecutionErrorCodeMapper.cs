using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

/// <summary> Maps <see cref="ExecutionErrorKind" /> values to command-facing machine-readable error codes. </summary>
internal static class ExecutionErrorCodeMapper
{
    /// <summary> Converts one execution-error kind to the corresponding CLI contract error code. </summary>
    /// <param name="kind"> The execution-error kind. </param>
    /// <returns> The mapped machine-readable error code. </returns>
    public static string ToCode (ExecutionErrorKind kind)
    {
        return kind switch
        {
            ExecutionErrorKind.InvalidArgument => IpcErrorCodes.InvalidArgument,
            ExecutionErrorKind.Timeout => ExecutionErrorCodes.IpcTimeout,
            ExecutionErrorKind.InternalError => IpcErrorCodes.InternalError,
            _ => IpcErrorCodes.InternalError,
        };
    }
}
