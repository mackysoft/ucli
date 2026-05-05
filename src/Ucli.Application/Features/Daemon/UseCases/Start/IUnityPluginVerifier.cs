using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;

/// <summary> Verifies that one Unity project contains the uCLI Unity plugin required by daemon startup. </summary>
internal interface IUnityPluginVerifier
{
    /// <summary> Verifies plugin availability for one Unity project root. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The plugin verification result. </returns>
    ValueTask<UnityPluginVerificationResult> Verify (
        string unityProjectRoot,
        CancellationToken cancellationToken = default);
}

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
