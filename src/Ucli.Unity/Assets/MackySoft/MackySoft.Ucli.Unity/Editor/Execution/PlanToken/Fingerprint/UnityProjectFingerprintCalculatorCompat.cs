using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Calculates deterministic project fingerprints with the same algorithm as CLI runtime. </summary>
    internal static class UnityProjectFingerprintCalculatorCompat
    {
        /// <summary> Creates one deterministic SHA-256 fingerprint for storage and project paths. </summary>
        /// <param name="storageRoot"> The storage root path. </param>
        /// <param name="unityProjectRoot"> The Unity project root path. </param>
        /// <returns> The lowercase hexadecimal SHA-256 string. </returns>
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
            var projectPathFragment = BuildProjectPathFragment(normalizedStorageRoot, normalizedUnityProjectRoot);
            var fingerprintInput = $"{normalizedStorageRoot}\n{projectPathFragment}";
            return PlanTokenSha256Hex.Compute(Encoding.UTF8.GetBytes(fingerprintInput));
        }

        /// <summary> Builds one stable project-path fragment used in fingerprint input. </summary>
        /// <param name="normalizedStorageRoot"> The normalized storage root. </param>
        /// <param name="normalizedUnityProjectRoot"> The normalized Unity project root. </param>
        /// <returns> The project path fragment. </returns>
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

            return normalizedUnityProjectRoot;
        }

        /// <summary> Normalizes one path used by fingerprint input. </summary>
        /// <param name="pathValue"> The path value. </param>
        /// <returns> The normalized path. </returns>
        private static string NormalizePath (string pathValue)
        {
            var fullPath = Path.GetFullPath(pathValue);
            fullPath = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var pathRoot = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(pathRoot) && string.Equals(fullPath, pathRoot, PathComparison))
            {
                return NormalizeCase(fullPath);
            }

            var trimmedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return NormalizeCase(trimmedPath);
        }

        /// <summary> Normalizes one relative path used by fingerprint input. </summary>
        /// <param name="relativePath"> The relative path value. </param>
        /// <returns> The normalized relative path. </returns>
        private static string NormalizeRelativePath (string relativePath)
        {
            var normalizedPath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (string.Equals(normalizedPath, ".", StringComparison.Ordinal))
            {
                return normalizedPath;
            }

            return normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary> Determines whether one path is under the specified directory path. </summary>
        /// <param name="path"> The candidate path. </param>
        /// <param name="directoryPath"> The directory path. </param>
        /// <returns> <see langword="true" /> when the path is under the directory. </returns>
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

        /// <summary> Normalizes path casing for platforms with case-insensitive path comparison. </summary>
        /// <param name="path"> The input path value. </param>
        /// <returns> The normalized path value. </returns>
        private static string NormalizeCase (string path)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? path.ToUpperInvariant()
                : path;
        }

        /// <summary> Gets path comparison mode for the current operating system. </summary>
        private static StringComparison PathComparison =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}