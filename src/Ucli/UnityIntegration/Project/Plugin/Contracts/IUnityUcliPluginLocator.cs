using MackySoft.FileSystem;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Contracts;

/// <summary> Locates the uCLI Unity plugin marker inside one Unity project. </summary>
internal interface IUnityUcliPluginLocator
{
    /// <summary> Locates the uCLI Unity plugin marker for one Unity project root. </summary>
    /// <param name="unityProjectRoot"> The absolute Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The plugin locate result. </returns>
    ValueTask<UnityUcliPluginLocateResult> LocateAsync (
        AbsolutePath unityProjectRoot,
        CancellationToken cancellationToken = default);
}
