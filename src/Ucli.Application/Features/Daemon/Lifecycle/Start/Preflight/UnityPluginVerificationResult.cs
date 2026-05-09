using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Preflight;

/// <summary> Represents the result of one uCLI Unity plugin verification attempt. </summary>
/// <param name="Error"> The structured verification error on failure; otherwise <see langword="null" />. </param>
internal sealed record UnityPluginVerificationResult (ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether verification succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful verification result. </summary>
    /// <returns> A successful verification result. </returns>
    public static UnityPluginVerificationResult Success ()
    {
        return new UnityPluginVerificationResult(Error: null);
    }

    /// <summary> Creates a failed verification result. </summary>
    /// <param name="error"> The structured verification error. </param>
    /// <returns> A failed verification result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityPluginVerificationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityPluginVerificationResult(error);
    }
}
