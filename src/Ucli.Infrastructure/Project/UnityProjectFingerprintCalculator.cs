using System.Text;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Project;

/// <summary> Calculates deterministic Unity-project fingerprints from storage and project paths. </summary>
public static class UnityProjectFingerprintCalculator
{
    /// <summary> Creates one deterministic SHA-256 fingerprint for storage-root and Unity-project identity values. </summary>
    /// <param name="storageRoot"> The normalized absolute storage root path. </param>
    /// <param name="unityProjectRoot"> The normalized absolute Unity project root path. </param>
    /// <returns> The canonical project fingerprint. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="unityProjectRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static ProjectFingerprint Create (
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

        return new ProjectFingerprint(Sha256LowerHex.Compute(normalizedBytes));
    }

    /// <summary> Builds a stable project-path fragment used for fingerprint input. </summary>
    /// <param name="normalizedStorageRoot"> The normalized storage-root path. </param>
    /// <param name="normalizedUnityProjectRoot"> The normalized Unity project root path. </param>
    /// <returns> The relative fragment when possible; otherwise the absolute Unity project root path. </returns>
    private static string BuildProjectPathFragment (
        string normalizedStorageRoot,
        string normalizedUnityProjectRoot)
    {
        if (PathIdentity.IsSamePath(normalizedStorageRoot, normalizedUnityProjectRoot))
        {
            return ".";
        }

        if (PathIdentity.IsChildPath(normalizedStorageRoot, normalizedUnityProjectRoot))
        {
            return NormalizeRelativePath(Path.GetRelativePath(normalizedStorageRoot, normalizedUnityProjectRoot));
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
        return PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(pathValue);
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
}
