using System.Runtime.InteropServices;
using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Paths;

namespace MackySoft.Ucli.Contracts.Project;

/// <summary> Calculates deterministic Unity-project fingerprints from storage and project paths. </summary>
public static class UnityProjectFingerprintCalculator
{
    /// <summary> Creates one deterministic SHA-256 fingerprint for storage-root and Unity-project identity values. </summary>
    /// <param name="storageRoot"> The normalized absolute storage root path. </param>
    /// <param name="unityProjectRoot"> The normalized absolute Unity project root path. </param>
    /// <returns> The lowercase hexadecimal SHA-256 string. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="unityProjectRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static string Create (
        string storageRoot,
        string unityProjectRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (string.IsNullOrWhiteSpace(unityProjectRoot))
        {
            throw new ArgumentException("Unity project root must not be empty.", nameof(unityProjectRoot));
        }

        var normalizedStorageRoot = NormalizePath(storageRoot);
        var normalizedUnityProjectRoot = NormalizePath(unityProjectRoot);
        var projectPathFragment = BuildProjectPathFragment(
            normalizedStorageRoot,
            normalizedUnityProjectRoot);
        var fingerprintInput = $"{normalizedStorageRoot}\n{projectPathFragment}";
        var normalizedBytes = Encoding.UTF8.GetBytes(fingerprintInput);

        return Sha256LowerHex.Compute(normalizedBytes);
    }

    /// <summary> Builds a stable project-path fragment used for fingerprint input. </summary>
    /// <param name="normalizedStorageRoot"> The normalized storage-root path. </param>
    /// <param name="normalizedUnityProjectRoot"> The normalized Unity project root path. </param>
    /// <returns> The relative fragment when possible; otherwise the absolute Unity project root path. </returns>
    private static string BuildProjectPathFragment (
        string normalizedStorageRoot,
        string normalizedUnityProjectRoot)
    {
        if (string.Equals(normalizedStorageRoot, normalizedUnityProjectRoot, PathComparison))
        {
            return ".";
        }

        if (IsUnderDirectory(normalizedUnityProjectRoot, normalizedStorageRoot))
        {
            var relativePath = Path.GetRelativePath(normalizedStorageRoot, normalizedUnityProjectRoot);
            return NormalizeRelativePath(relativePath);
        }

        // NOTE:
        // Unity project path is expected to be equal to or under storage root.
        // Keep deterministic behavior even for unexpected directory layouts.
        return normalizedUnityProjectRoot;
    }

    /// <summary> Normalizes path values used in fingerprint input. </summary>
    /// <param name="pathValue"> The path value. </param>
    /// <returns> The normalized path value. </returns>
    private static string NormalizePath (string pathValue)
    {
        var fullPath = Path.GetFullPath(pathValue);
        fullPath = PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(fullPath);
        var pathRoot = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(pathRoot) && string.Equals(fullPath, pathRoot, PathComparison))
        {
            return NormalizeCase(fullPath);
        }

        var trimmedPath = PathStringNormalizer.TrimTrailingDirectorySeparators(fullPath);
        return NormalizeCase(trimmedPath);
    }

    /// <summary> Normalizes relative path fragments used for fingerprint input. </summary>
    /// <param name="relativePath"> The relative path fragment. </param>
    /// <returns> The normalized fragment value. </returns>
    private static string NormalizeRelativePath (string relativePath)
    {
        var normalizedPath = PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(relativePath);
        if (string.Equals(normalizedPath, ".", StringComparison.Ordinal))
        {
            return normalizedPath;
        }

        return PathStringNormalizer.TrimTrailingDirectorySeparators(normalizedPath);
    }

    /// <summary> Determines whether <paramref name="path" /> is located under <paramref name="directoryPath" />. </summary>
    /// <param name="path"> The candidate absolute path. </param>
    /// <param name="directoryPath"> The parent directory absolute path. </param>
    /// <returns> <see langword="true" /> when <paramref name="path" /> is under <paramref name="directoryPath" />; otherwise <see langword="false" />. </returns>
    private static bool IsUnderDirectory (
        string path,
        string directoryPath)
    {
        if (!path.StartsWith(directoryPath, PathComparison))
        {
            return false;
        }

        var trailingDirectoryPath = directoryPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || directoryPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? directoryPath
            : directoryPath + Path.DirectorySeparatorChar;
        return path.StartsWith(trailingDirectoryPath, PathComparison);
    }

    /// <summary> Normalizes path casing for platforms with case-insensitive paths. </summary>
    /// <param name="path"> The path value to normalize. </param>
    /// <returns> The normalized path string. </returns>
    private static string NormalizeCase (string path)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? path.ToUpperInvariant()
            : path;
    }

    /// <summary> Gets the path comparison mode for the current operating system. </summary>
    private static StringComparison PathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

}