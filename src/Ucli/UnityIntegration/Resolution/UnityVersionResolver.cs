using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Resolves Unity version values from preferred input and <c>ProjectVersion.txt</c>. </summary>
internal sealed class UnityVersionResolver : IUnityVersionResolver
{
    /// <summary> Resolves the effective Unity version from preferred input and project metadata. </summary>
    /// <param name="projectPath"> The Unity project root path. </param>
    /// <param name="preferredUnityVersion"> The preferred Unity version value. </param>
    /// <returns> The Unity-version resolution result. </returns>
    public UnityVersionResolutionResult Resolve (
        AbsolutePath projectPath,
        string? preferredUnityVersion)
    {
        if (!string.IsNullOrWhiteSpace(preferredUnityVersion))
        {
            return UnityVersionResolutionResult.Success(preferredUnityVersion.Trim());
        }

        var projectVersionPath = UnityProjectVersionFileReader.GetProjectVersionPath(projectPath);
        if (!File.Exists(projectVersionPath.Value))
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"ProjectVersion.txt does not exist: {projectVersionPath.Value}"));
        }

        var readResult = UnityProjectVersionFileReader.ReadEditorVersion(projectVersionPath);
        if (readResult.Status == UnityProjectVersionFileReader.ReadStatus.Success)
        {
            return UnityVersionResolutionResult.Success(readResult.UnityVersion!);
        }

        if (readResult.Status == UnityProjectVersionFileReader.ReadStatus.MissingEditorVersion)
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"m_EditorVersion is missing or invalid in: {projectVersionPath.Value}"));
        }

        return UnityVersionResolutionResult.Failure(ExecutionError.InternalError(
            $"Failed to read ProjectVersion.txt. {readResult.ErrorMessage}"));
    }
}
