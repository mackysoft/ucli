using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Session;

/// <summary> Represents the result of one daemon session storage write or delete operation. </summary>
/// <param name="Error"> The structured error when operation fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionStoreOperationResult (ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful operation result. </summary>
    /// <returns> The successful operation result. </returns>
    public static DaemonSessionStoreOperationResult Success ()
    {
        return new DaemonSessionStoreOperationResult((ExecutionError?)null);
    }

    /// <summary> Creates a failed operation result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonSessionStoreOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonSessionStoreOperationResult(error);
    }
}