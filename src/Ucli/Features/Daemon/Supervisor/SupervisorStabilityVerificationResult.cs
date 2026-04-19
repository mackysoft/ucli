using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor;

/// <summary> Represents the outcome of supervisor stability verification for one daemon session. </summary>
internal sealed record SupervisorStabilityVerificationResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether stability verification succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates one successful stability-verification result. </summary>
    /// <returns> The successful result. </returns>
    public static SupervisorStabilityVerificationResult Success ()
    {
        return new SupervisorStabilityVerificationResult((ExecutionError?)null);
    }

    /// <summary> Creates one failed stability-verification result. </summary>
    /// <param name="error"> The structured verification error. </param>
    /// <returns> The failed result. </returns>
    public static SupervisorStabilityVerificationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SupervisorStabilityVerificationResult(error);
    }
}