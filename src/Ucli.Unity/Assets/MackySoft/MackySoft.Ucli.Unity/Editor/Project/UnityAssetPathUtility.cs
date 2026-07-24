using System;
using System.IO;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;

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

            return NormalizeProjectRelativeSeparators(directoryPath);
        }

        /// <summary> Normalizes Unity project-relative path separators to <c>/</c>. </summary>
        /// <param name="projectRelativePath"> The Unity project-relative path text. </param>
        /// <returns> The path text using Unity's canonical separator. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectRelativePath" /> is <see langword="null" />. </exception>
        public static string NormalizeProjectRelativeSeparators (string projectRelativePath)
        {
            if (projectRelativePath == null)
            {
                throw new ArgumentNullException(nameof(projectRelativePath));
            }

            return projectRelativePath.Replace('\\', '/');
        }

        /// <summary> Resolves one Unity project-relative path to a guarded absolute path. </summary>
        /// <param name="projectRelativePath"> The Unity project-relative path. </param>
        /// <returns> The guarded path under the current Unity project root. </returns>
        public static AbsolutePath ResolveProjectRelativePath (string projectRelativePath)
        {
            var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
            var relativePath = RootRelativePath.Parse(
                NormalizeProjectRelativeSeparators(projectRelativePath));
            return ContainedPath.Create(projectRoot, relativePath).Target;
        }

    }
}
