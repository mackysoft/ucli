using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

/// <summary> Maps <see cref="ExecutionErrorKind" /> values to command-facing machine-readable error codes. </summary>
internal static class ExecutionErrorCodeMapper
{
    /// <summary> Converts one execution error to the corresponding CLI contract error code. </summary>
    /// <param name="error"> The execution error. </param>
    /// <returns> The mapped machine-readable error code. </returns>
    public static UcliErrorCode ToCode (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.Code.HasValue && error.Code.Value.IsValid
            ? error.Code.Value
            : ToCode(error.Kind);
    }

    /// <summary> Converts one execution-error kind to the corresponding CLI contract error code. </summary>
    /// <param name="kind"> The execution-error kind. </param>
    /// <returns> The mapped machine-readable error code. </returns>
    public static UcliErrorCode ToCode (ExecutionErrorKind kind)
    {
        return kind switch
        {
            ExecutionErrorKind.InvalidArgument => UcliCoreErrorCodes.InvalidArgument,
            ExecutionErrorKind.Timeout => ExecutionErrorCodes.IpcTimeout,
            ExecutionErrorKind.InternalError => UcliCoreErrorCodes.InternalError,
            _ => UcliCoreErrorCodes.InternalError,
        };
    }
}
