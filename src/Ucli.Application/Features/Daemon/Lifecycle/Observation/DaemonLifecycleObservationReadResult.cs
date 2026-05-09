using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents the result of reading daemon lifecycle observation metadata. </summary>
internal sealed record DaemonLifecycleObservationReadResult (
    bool IsSuccess,
    bool Exists,
    DaemonLifecycleObservation? Observation,
    ExecutionError? Error)
{
    public static DaemonLifecycleObservationReadResult Success (DaemonLifecycleObservation? observation)
    {
        return new DaemonLifecycleObservationReadResult(true, observation is not null, observation, null);
    }

    public static DaemonLifecycleObservationReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLifecycleObservationReadResult(false, false, null, error);
    }
}
