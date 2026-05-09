using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents the result of daemon lifecycle observation store mutation. </summary>
internal sealed record DaemonLifecycleStoreOperationResult (
    bool IsSuccess,
    ExecutionError? Error)
{
    public static DaemonLifecycleStoreOperationResult Success ()
    {
        return new DaemonLifecycleStoreOperationResult(true, null);
    }

    public static DaemonLifecycleStoreOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLifecycleStoreOperationResult(false, error);
    }
}
