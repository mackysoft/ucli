using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Locates Unity editor executable paths from preferred values or default installation roots. </summary>
internal static class UnityEditorExecutablePathLocator
{
    private static readonly RootRelativePath[] ExecutableRelativePaths =
    {
        RootRelativePath.Parse("Contents/MacOS/Unity"),
        RootRelativePath.Parse("Unity.app/Contents/MacOS/Unity"),
        RootRelativePath.Parse("Editor/Unity.exe"),
        RootRelativePath.Parse("Editor/Unity"),
        RootRelativePath.Parse("Unity.exe"),
        RootRelativePath.Parse("Unity"),
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
    public static UnityEditorPathResolutionResult Resolve (
        string unityVersion,
        string? preferredUnityEditorPath,
        IReadOnlyList<AbsolutePath> searchRoots)
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
        var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
        if (!AbsolutePath.TryResolve(currentDirectory, preferredUnityEditorPath, out var normalizedPath, out _))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Unity editor path is invalid: {preferredUnityEditorPath}"));
        }

        if (File.Exists(normalizedPath.Value))
        {
            if (!IsSupportedExecutableFileName(normalizedPath))
            {
                return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                    $"unityEditorPath must point to a Unity executable (Unity or Unity.exe): {normalizedPath.Value}"));
            }

            return UnityEditorPathResolutionResult.Success(normalizedPath);
        }

        if (!Directory.Exists(normalizedPath.Value))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityEditorPath does not exist: {normalizedPath.Value}"));
        }

        if (!TryResolveExecutablePath(normalizedPath, out var executablePath))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityEditorPath does not contain a Unity executable: {normalizedPath.Value}"));
        }

        return UnityEditorPathResolutionResult.Success(executablePath);
    }

    /// <summary> Resolves one Unity editor executable path from search roots for one target version. </summary>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <param name="searchRoots"> Candidate root directories for Unity editor installations. </param>
    /// <returns> The executable-path resolution result. </returns>
    private static UnityEditorPathResolutionResult ResolveFromSearchRoots (
        string unityVersion,
        IReadOnlyList<AbsolutePath> searchRoots)
    {
        if (!RootRelativePath.TryParse(unityVersion, out var versionRelativePath, out _))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Unity version cannot be used as an installation directory name: {unityVersion}"));
        }

        foreach (var searchRoot in searchRoots)
        {
            ArgumentNullException.ThrowIfNull(searchRoot);
            var versionDirectoryPath = ContainedPath.Create(searchRoot, versionRelativePath).Target;
            if (!Directory.Exists(versionDirectoryPath.Value))
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
        AbsolutePath directoryPath,
        [NotNullWhen(true)] out AbsolutePath? executablePath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        executablePath = null;

        foreach (var relativePath in ExecutableRelativePaths)
        {
            var candidatePath = ContainedPath.Create(directoryPath, relativePath).Target;
            if (!File.Exists(candidatePath.Value))
            {
                continue;
            }

            if (!IsSupportedExecutableFileName(candidatePath))
            {
                continue;
            }

            executablePath = candidatePath;
            return true;
        }

        return false;
    }

    /// <summary> Determines whether one executable file name is supported as a Unity editor binary. </summary>
    /// <param name="filePath"> The file path to inspect. </param>
    /// <returns> <see langword="true" /> when the file name matches supported Unity binaries; otherwise <see langword="false" />. </returns>
    private static bool IsSupportedExecutableFileName (AbsolutePath filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var fileName = Path.GetFileName(filePath.Value);
        for (var index = 0; index < SupportedExecutableFileNames.Length; index++)
        {
            if (string.Equals(fileName, SupportedExecutableFileNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}
