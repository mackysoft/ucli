using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Resolves Unity version values from preferred input and <c>ProjectVersion.txt</c>. </summary>
internal sealed class UnityVersionResolver : IUnityVersionResolver
{
    private const string ProjectSettingsDirectoryName = "ProjectSettings";

    private const string ProjectVersionFileName = "ProjectVersion.txt";

    private const string EditorVersionPrefix = "m_EditorVersion:";

    /// <summary> Resolves the effective Unity version from preferred input and project metadata. </summary>
    /// <param name="projectPath"> The Unity project root path. </param>
    /// <param name="preferredUnityVersion"> The preferred Unity version value. </param>
    /// <returns> The Unity-version resolution result. </returns>
    public UnityVersionResolutionResult Resolve (
        string projectPath,
        string? preferredUnityVersion)
    {
        if (!string.IsNullOrWhiteSpace(preferredUnityVersion))
        {
            return UnityVersionResolutionResult.Success(preferredUnityVersion.Trim());
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InvalidArgument(
                "Unity project path must not be null, empty, or whitespace."));
        }

        string normalizedProjectPath;
        try
        {
            normalizedProjectPath = Path.GetFullPath(projectPath);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Unity project path is invalid: {projectPath}"));
        }

        var projectVersionPath = Path.Combine(
            normalizedProjectPath,
            ProjectSettingsDirectoryName,
            ProjectVersionFileName);
        if (!File.Exists(projectVersionPath))
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"ProjectVersion.txt does not exist: {projectVersionPath}"));
        }

        string content;
        try
        {
            content = File.ReadAllText(projectVersionPath);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"ProjectVersion.txt path is invalid: {projectVersionPath}. {exception.Message}"));
        }
        catch (UnauthorizedAccessException exception)
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InternalError(
                $"Failed to read ProjectVersion.txt. {exception.Message}"));
        }
        catch (IOException exception)
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InternalError(
                $"Failed to read ProjectVersion.txt. {exception.Message}"));
        }

        if (!TryGetEditorVersion(content, out var unityVersion))
        {
            return UnityVersionResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"m_EditorVersion is missing or invalid in: {projectVersionPath}"));
        }

        return UnityVersionResolutionResult.Success(unityVersion);
    }

    /// <summary> Tries to extract one editor version value from <c>ProjectVersion.txt</c> contents. </summary>
    /// <param name="content"> The full file contents. </param>
    /// <param name="unityVersion"> The extracted Unity version. </param>
    /// <returns> <see langword="true" /> when extraction succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryGetEditorVersion (
        string content,
        out string unityVersion)
    {
        unityVersion = string.Empty;

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!line.StartsWith(EditorVersionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[EditorVersionPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            unityVersion = value;
            return true;
        }

        return false;
    }
}
