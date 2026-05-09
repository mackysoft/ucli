namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Preflight;

/// <summary> Verifies that one Unity project contains the uCLI Unity plugin required by daemon startup. </summary>
internal interface IUnityPluginVerifier
{
    /// <summary> Verifies plugin availability for one Unity project root. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The plugin verification result. </returns>
    ValueTask<UnityPluginVerificationResult> VerifyAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken = default);
}
