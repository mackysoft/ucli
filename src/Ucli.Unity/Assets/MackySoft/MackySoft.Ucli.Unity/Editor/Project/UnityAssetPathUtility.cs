using System;
using System.IO;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Paths;

#nullable enable

namespace MackySoft.Ucli.Unity.Project
{
    /// <summary> Provides Unity Editor filesystem projections for normalized Unity asset paths. </summary>
    internal static class UnityAssetPathUtility
    {
        /// <summary> Resolves the parent directory path for one normalized Unity asset path. </summary>
        /// <param name="normalizedAssetPath"> The normalized Unity asset path. </param>
        /// <returns> The normalized parent directory path, or <c>Assets</c> for root-level assets. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="normalizedAssetPath" /> is <see langword="null" />. </exception>
        public static string ResolveDirectoryPath (string normalizedAssetPath)
        {
            if (normalizedAssetPath == null)
            {
                throw new ArgumentNullException(nameof(normalizedAssetPath));
            }

            var directoryPath = Path.GetDirectoryName(normalizedAssetPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return UnityAssetPathContract.AssetsRootPath;
            }

            return PathStringNormalizer.ToSlashSeparated(directoryPath);
        }

        /// <summary> Resolves one normalized Unity asset path to its absolute filesystem path. </summary>
        /// <param name="normalizedAssetPath"> The normalized Unity asset path. </param>
        /// <returns> The absolute filesystem path. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="normalizedAssetPath" /> is <see langword="null" />. </exception>
        public static string ToAbsolutePath (string normalizedAssetPath)
        {
            if (normalizedAssetPath == null)
            {
                throw new ArgumentNullException(nameof(normalizedAssetPath));
            }

            return Path.Combine(
                UnityProjectPathResolver.ResolveProjectRootPath(),
                PathStringNormalizer.ToPlatformSeparated(normalizedAssetPath));
        }
    }
}
