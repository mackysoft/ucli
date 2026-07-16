using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Defines Unity batchmode process launch options that are not part of the uCLI bootstrap payload. </summary>
/// <param name="ActiveBuildProfilePath"> The optional project-relative Unity Build Profile asset path passed to Unity <c>-activeBuildProfile</c>. </param>
internal sealed record UnityBatchmodeLaunchOptions (
    UnityBuildProfileAssetPath? ActiveBuildProfilePath)
{
    /// <summary> Gets the default launch options. </summary>
    public static UnityBatchmodeLaunchOptions Default { get; } = new(ActiveBuildProfilePath: null);
}
