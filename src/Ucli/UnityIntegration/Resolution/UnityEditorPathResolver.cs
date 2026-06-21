using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Resolves Unity editor executable paths that satisfy target Unity version constraints. </summary>
internal sealed class UnityEditorPathResolver : IUnityEditorPathResolver
{
    /// <summary> Resolves an editor executable path that matches the specified Unity version. </summary>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <param name="preferredUnityEditorPath"> The preferred editor path value. </param>
    /// <returns> The editor-path resolution result. </returns>
    public UnityEditorPathResolutionResult Resolve (
        string unityVersion,
        string? preferredUnityEditorPath)
    {
        if (string.IsNullOrWhiteSpace(unityVersion))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                "Unity version must not be null, empty, or whitespace."));
        }

        var normalizedUnityVersion = unityVersion.Trim();
        var executablePathResult = UnityEditorExecutablePathLocator.Resolve(
            normalizedUnityVersion,
            preferredUnityEditorPath,
            UnityEditorInstallationSearchRoots.GetSearchRoots());
        if (!executablePathResult.IsSuccess)
        {
            return executablePathResult;
        }

        return UnityEditorVersionConsistencyValidator.Validate(
            executablePathResult.UnityEditorPath!,
            normalizedUnityVersion);
    }
}
