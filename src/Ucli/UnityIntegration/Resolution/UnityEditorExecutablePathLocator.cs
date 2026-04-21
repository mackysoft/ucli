using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Locates Unity editor executable paths from preferred values or default installation roots. </summary>
internal sealed class UnityEditorExecutablePathLocator
{
    private static readonly string[] ExecutableRelativePaths =
    {
        Path.Combine("Contents", "MacOS", "Unity"),
        Path.Combine("Unity.app", "Contents", "MacOS", "Unity"),
        Path.Combine("Editor", "Unity.exe"),
        Path.Combine("Editor", "Unity"),
        "Unity.exe",
        "Unity",
    };

    private static readonly string[] SupportedExecutableFileNames =
    {
        "Unity",
        "Unity.exe",
    };

    /// <summary> Resolves one Unity editor executable path from preferred values or search roots. </summary>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <param name="preferredUnityEditorPath"> The preferred editor path value. </param>
    /// <param name="searchRoots"> Candidate root directories for Unity editor installations. </param>
    /// <returns> The executable-path resolution result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRoots" /> is <see langword="null" />. </exception>
    public UnityEditorPathResolutionResult Resolve (
        string unityVersion,
        string? preferredUnityEditorPath,
        IReadOnlyList<string> searchRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityVersion);
        ArgumentNullException.ThrowIfNull(searchRoots);

        if (!string.IsNullOrWhiteSpace(preferredUnityEditorPath))
        {
            return ResolvePreferredPath(preferredUnityEditorPath);
        }

        return ResolveFromSearchRoots(unityVersion, searchRoots);
    }

    /// <summary> Resolves one preferred editor path value to a normalized executable path. </summary>
    /// <param name="preferredUnityEditorPath"> The preferred editor path value. </param>
    /// <returns> The executable-path resolution result. </returns>
    private static UnityEditorPathResolutionResult ResolvePreferredPath (string preferredUnityEditorPath)
    {
        if (!TryNormalizePath(preferredUnityEditorPath, out var normalizedPath))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Unity editor path is invalid: {preferredUnityEditorPath}"));
        }

        if (File.Exists(normalizedPath))
        {
            if (!IsSupportedExecutableFileName(normalizedPath))
            {
                return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                    $"unityEditorPath must point to a Unity executable (Unity or Unity.exe): {normalizedPath}"));
            }

            return UnityEditorPathResolutionResult.Success(normalizedPath);
        }

        if (!Directory.Exists(normalizedPath))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityEditorPath does not exist: {normalizedPath}"));
        }

        if (!TryResolveExecutablePath(normalizedPath, out var executablePath))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityEditorPath does not contain a Unity executable: {normalizedPath}"));
        }

        return UnityEditorPathResolutionResult.Success(executablePath);
    }

    /// <summary> Resolves one Unity editor executable path from search roots for one target version. </summary>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <param name="searchRoots"> Candidate root directories for Unity editor installations. </param>
    /// <returns> The executable-path resolution result. </returns>
    private static UnityEditorPathResolutionResult ResolveFromSearchRoots (
        string unityVersion,
        IReadOnlyList<string> searchRoots)
    {
        foreach (var searchRoot in searchRoots)
        {
            if (string.IsNullOrWhiteSpace(searchRoot))
            {
                continue;
            }

            if (!TryNormalizePath(searchRoot, out var normalizedSearchRoot))
            {
                continue;
            }

            var versionDirectoryPath = Path.Combine(normalizedSearchRoot, unityVersion);
            if (!Directory.Exists(versionDirectoryPath))
            {
                continue;
            }

            if (!TryResolveExecutablePath(versionDirectoryPath, out var executablePath))
            {
                continue;
            }

            return UnityEditorPathResolutionResult.Success(executablePath);
        }

        return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
            $"Unity Editor is not installed for unityVersion '{unityVersion}'."));
    }

    /// <summary> Tries to resolve one executable path from one directory path. </summary>
    /// <param name="directoryPath"> The directory path to inspect. </param>
    /// <param name="executablePath"> The resolved executable path. </param>
    /// <returns> <see langword="true" /> when an executable path is resolved; otherwise <see langword="false" />. </returns>
    private static bool TryResolveExecutablePath (
        string directoryPath,
        out string executablePath)
    {
        executablePath = string.Empty;

        foreach (var relativePath in ExecutableRelativePaths)
        {
            var candidatePath = Path.Combine(directoryPath, relativePath);
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            if (!TryNormalizePath(candidatePath, out executablePath))
            {
                executablePath = string.Empty;
                continue;
            }

            if (!IsSupportedExecutableFileName(executablePath))
            {
                executablePath = string.Empty;
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary> Determines whether one executable file name is supported as a Unity editor binary. </summary>
    /// <param name="filePath"> The file path to inspect. </param>
    /// <returns> <see langword="true" /> when the file name matches supported Unity binaries; otherwise <see langword="false" />. </returns>
    private static bool IsSupportedExecutableFileName (string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        for (var index = 0; index < SupportedExecutableFileNames.Length; index++)
        {
            if (string.Equals(fileName, SupportedExecutableFileNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary> Tries to normalize one path value into an absolute path. </summary>
    /// <param name="pathValue"> The path value to normalize. </param>
    /// <param name="normalizedPath"> The normalized absolute path on success. </param>
    /// <returns> <see langword="true" /> when normalization succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryNormalizePath (
        string pathValue,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        try
        {
            normalizedPath = Path.GetFullPath(pathValue);
            return true;
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return false;
        }
    }
}