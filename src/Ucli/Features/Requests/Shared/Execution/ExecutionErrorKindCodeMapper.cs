using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Shared.Execution;

/// <summary> Maps <see cref="ExecutionErrorKind" /> values to command-facing machine-readable error codes. </summary>
internal static class ExecutionErrorKindCodeMapper
{
    /// <summary> Converts one execution-error kind to the corresponding CLI contract error code. </summary>
    /// <param name="kind"> The execution-error kind. </param>
    /// <returns> The mapped machine-readable error code. </returns>
    public static string ToCode (ExecutionErrorKind kind)
    {
        return kind switch
        {
            ExecutionErrorKind.InvalidArgument => IpcErrorCodes.InvalidArgument,
            ExecutionErrorKind.Timeout => CliErrorCodes.IpcTimeout,
            ExecutionErrorKind.InternalError => IpcErrorCodes.InternalError,
            _ => IpcErrorCodes.InternalError,
        };
    }
}