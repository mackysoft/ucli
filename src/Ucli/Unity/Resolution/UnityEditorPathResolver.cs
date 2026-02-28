using System.Text.RegularExpressions;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Resolves Unity editor executable paths from preferred values or default installation roots. </summary>
internal sealed class UnityEditorPathResolver : IUnityEditorPathResolver
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

    private static readonly Regex UnityVersionRegex = new(
        @"^\d+\.\d+\.\d+[abcfp]\d+(?:c\d+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] SupportedExecutableFileNames =
    {
        "Unity",
        "Unity.exe",
    };

    private readonly IUnityEditorSearchRootProvider searchRootProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityEditorPathResolver" /> class. </summary>
    /// <param name="searchRootProvider"> The search-root provider dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootProvider" /> is <see langword="null" />. </exception>
    public UnityEditorPathResolver (IUnityEditorSearchRootProvider searchRootProvider)
    {
        this.searchRootProvider = searchRootProvider ?? throw new ArgumentNullException(nameof(searchRootProvider));
    }

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

        if (!string.IsNullOrWhiteSpace(preferredUnityEditorPath))
        {
            var preferredPathResolutionResult = ResolvePreferredPath(preferredUnityEditorPath);
            if (!preferredPathResolutionResult.IsSuccess)
            {
                return preferredPathResolutionResult;
            }

            return ValidateVersionConsistency(
                preferredPathResolutionResult.UnityEditorPath!,
                normalizedUnityVersion);
        }

        foreach (var searchRoot in searchRootProvider.GetSearchRoots())
        {
            if (string.IsNullOrWhiteSpace(searchRoot))
            {
                continue;
            }

            if (!TryNormalizePath(searchRoot, out var normalizedSearchRoot))
            {
                continue;
            }

            var versionDirectoryPath = Path.Combine(normalizedSearchRoot, normalizedUnityVersion);
            if (!Directory.Exists(versionDirectoryPath))
            {
                continue;
            }

            if (!TryResolveExecutablePath(versionDirectoryPath, out var executablePath))
            {
                continue;
            }

            return ValidateVersionConsistency(executablePath, normalizedUnityVersion);
        }

        return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
            $"Unity Editor is not installed for unityVersion '{normalizedUnityVersion}'."));
    }

    /// <summary> Resolves one preferred editor path value to an executable path. </summary>
    /// <param name="preferredUnityEditorPath"> The preferred editor path value. </param>
    /// <returns> The preferred-path resolution result. </returns>
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

    /// <summary> Resolves one executable path from one directory path. </summary>
    /// <param name="directoryPath"> The directory path to inspect. </param>
    /// <param name="executablePath"> The resolved executable path. </param>
    /// <returns> <see langword="true" /> when an executable path is resolved. </returns>
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

    /// <summary> Validates that one editor path contains the same version as the target Unity version. </summary>
    /// <param name="unityEditorPath"> The editor executable path. </param>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <returns> The validation result as editor-path resolution output. </returns>
    private static UnityEditorPathResolutionResult ValidateVersionConsistency (
        string unityEditorPath,
        string unityVersion)
    {
        if (!TryGetVersionFromPath(unityEditorPath, out var detectedVersion))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityEditorPath version cannot be determined from standard layout: {unityEditorPath}"));
        }

        if (!string.Equals(detectedVersion, unityVersion, StringComparison.Ordinal))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityVersion '{unityVersion}' conflicts with unityEditorPath version '{detectedVersion}'."));
        }

        return UnityEditorPathResolutionResult.Success(unityEditorPath);
    }

    /// <summary> Tries to extract one Unity version value from one editor path. </summary>
    /// <param name="unityEditorPath"> The editor executable path. </param>
    /// <param name="detectedVersion"> The detected version value. </param>
    /// <returns> <see langword="true" /> when one version value is detected; otherwise <see langword="false" />. </returns>
    private static bool TryGetVersionFromPath (
        string unityEditorPath,
        out string detectedVersion)
    {
        detectedVersion = string.Empty;

        var normalizedPath = unityEditorPath.Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            if (!UnityVersionRegex.IsMatch(segments[index]))
            {
                continue;
            }

            var previousSegment = index > 0 ? segments[index - 1] : null;
            var nextSegment = index < segments.Length - 1 ? segments[index + 1] : null;
            if (string.Equals(previousSegment, "Editor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nextSegment, "Editor", StringComparison.OrdinalIgnoreCase))
            {
                detectedVersion = segments[index];
                return true;
            }
        }

        return false;
    }

    /// <summary> Determines whether one executable file name is supported as a Unity editor binary. </summary>
    /// <param name="filePath"> The file path to inspect. </param>
    /// <returns> <see langword="true" /> when the file name matches supported Unity binaries. </returns>
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
    /// <returns> <see langword="true" /> when normalization succeeds. </returns>
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
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return false;
        }
    }

    /// <summary> Determines whether one exception indicates invalid path formatting. </summary>
    /// <param name="exception"> The exception to inspect. </param>
    /// <returns> <see langword="true" /> when the exception indicates invalid path formatting. </returns>
    private static bool IsPathFormatException (Exception exception)
    {
        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }
}