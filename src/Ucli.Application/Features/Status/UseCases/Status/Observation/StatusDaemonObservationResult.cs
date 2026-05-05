using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;

/// <summary> Represents the result of observing daemon status and runtime diagnostics. </summary>
/// <param name="Observation"> The normalized daemon observation values, or <see langword="null" /> on failure. </param>
/// <param name="Error"> The structured error, or <see langword="null" /> on success. </param>
internal sealed record StatusDaemonObservationResult (
    StatusDaemonObservation? Observation,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon observation succeeded. </summary>
    public bool IsSuccess => Observation is not null && Error is null;

    /// <summary> Creates a successful daemon observation result. </summary>
    /// <param name="observation"> The normalized daemon observation values. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="observation" /> is <see langword="null" />. </exception>
    public static StatusDaemonObservationResult Success (StatusDaemonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new StatusDaemonObservationResult(observation, null);
    }

    /// <summary> Creates a failed daemon observation result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static StatusDaemonObservationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new StatusDaemonObservationResult(null, error);
    }
}
