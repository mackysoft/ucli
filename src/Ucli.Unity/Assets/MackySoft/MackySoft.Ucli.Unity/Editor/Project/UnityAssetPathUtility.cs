using System;
using System.IO;
using MackySoft.Ucli.Infrastructure.Paths;

#nullable enable

namespace MackySoft.Ucli.Unity.Project
{
    /// <summary> Provides shared helpers for Unity asset-path normalization and root handling. </summary>
    internal static class UnityAssetPathUtility
    {
        /// <summary> The canonical <c>Assets</c> root path. </summary>
        public const string AssetsRootPath = "Assets";

        /// <summary> The canonical <c>Assets/</c> root prefix. </summary>
        public const string AssetsRootPrefix = "Assets/";

        private const string SceneExtension = ".unity";

        private const string PrefabExtension = ".prefab";

        /// <summary> Normalizes one asset path to slash-separated protocol form. </summary>
        /// <param name="assetPath"> The source asset path. </param>
        /// <returns> The normalized asset path. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetPath" /> is <see langword="null" />. </exception>
        public static string NormalizeAssetPath (string assetPath)
        {
            if (assetPath == null)
            {
                throw new ArgumentNullException(nameof(assetPath));
            }

            return PathStringNormalizer.ToSlashSeparated(assetPath);
        }

        /// <summary> Returns whether the path points at the <c>Assets</c> root or one of its descendants. </summary>
        /// <param name="assetPath"> The candidate asset path. </param>
        /// <returns> <see langword="true" /> when the path is under the <c>Assets</c> root; otherwise <see langword="false" />. </returns>
        public static bool IsAssetsRootOrDescendant (string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            return string.Equals(normalizedAssetPath, AssetsRootPath, StringComparison.Ordinal)
                || normalizedAssetPath.StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
        }

        /// <summary> Returns whether the path points at one asset entry under <c>Assets/</c>. </summary>
        /// <param name="assetPath"> The candidate asset path. </param>
        /// <returns> <see langword="true" /> when the path is a descendant of <c>Assets/</c>; otherwise <see langword="false" />. </returns>
        public static bool IsAssetsDescendantPath (string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            return NormalizeAssetPath(assetPath).StartsWith(AssetsRootPrefix, StringComparison.Ordinal);
        }

        /// <summary> Returns whether the asset path is reserved for scene or prefab domain operations. </summary>
        /// <param name="assetPath"> The candidate asset path. </param>
        /// <returns> <see langword="true" /> when the path uses a reserved extension; otherwise <see langword="false" />. </returns>
        public static bool IsReservedAssetPath (string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            return normalizedAssetPath.EndsWith(SceneExtension, StringComparison.Ordinal)
                || normalizedAssetPath.EndsWith(PrefabExtension, StringComparison.Ordinal);
        }

        /// <summary> Resolves the parent directory path for one asset path. </summary>
        /// <param name="assetPath"> The asset path. </param>
        /// <returns> The normalized parent directory path, or <c>Assets</c> for root-level assets. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetPath" /> is <see langword="null" />. </exception>
        public static string ResolveDirectoryPath (string assetPath)
        {
            if (assetPath == null)
            {
                throw new ArgumentNullException(nameof(assetPath));
            }

            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            var directoryPath = Path.GetDirectoryName(normalizedAssetPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return AssetsRootPath;
            }

            return NormalizeAssetPath(directoryPath);
        }

        /// <summary> Resolves one Unity asset path to its absolute filesystem path. </summary>
        /// <param name="assetPath"> The Unity asset path. </param>
        /// <returns> The absolute filesystem path. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetPath" /> is <see langword="null" />. </exception>
        public static string ToAbsolutePath (string assetPath)
        {
            if (assetPath == null)
            {
                throw new ArgumentNullException(nameof(assetPath));
            }

            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            return Path.Combine(
                UnityProjectPathResolver.ResolveProjectRootPath(),
                PathStringNormalizer.ToPlatformSeparated(normalizedAssetPath));
        }
    }
}