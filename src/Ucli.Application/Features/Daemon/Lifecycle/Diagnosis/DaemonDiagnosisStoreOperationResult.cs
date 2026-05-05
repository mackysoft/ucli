using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Represents the result of one daemon diagnosis storage operation. </summary>
/// <param name="Error"> The structured error when operation fails. </param>
internal sealed record DaemonDiagnosisStoreOperationResult (ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates one successful diagnosis storage result. </summary>
    /// <returns> The successful result. </returns>
    public static DaemonDiagnosisStoreOperationResult Success ()
    {
        return new DaemonDiagnosisStoreOperationResult((ExecutionError?)null);
    }

    /// <summary> Creates one failed diagnosis storage result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonDiagnosisStoreOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonDiagnosisStoreOperationResult(error);
    }
}
