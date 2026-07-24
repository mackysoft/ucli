using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Preflight;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin;

/// <summary> Adapts Unity plugin discovery to the daemon-start application port. </summary>
internal sealed class UnityPluginVerifier : IUnityPluginVerifier
{
    private readonly IUnityUcliPluginLocator pluginLocator;

    /// <summary> Initializes a new instance of the <see cref="UnityPluginVerifier" /> class. </summary>
    /// <param name="pluginLocator"> The Unity plugin locator dependency. </param>
    public UnityPluginVerifier (IUnityUcliPluginLocator pluginLocator)
    {
        this.pluginLocator = pluginLocator ?? throw new ArgumentNullException(nameof(pluginLocator));
    }

    /// <inheritdoc />
    public async ValueTask<UnityPluginVerificationResult> VerifyAsync (
        AbsolutePath unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        var locateResult = await pluginLocator.LocateAsync(unityProjectRoot, cancellationToken).ConfigureAwait(false);
        return locateResult.IsSuccess
            ? UnityPluginVerificationResult.Success()
            : UnityPluginVerificationResult.Failure(locateResult.Error!);
    }
}
