using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Paths;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves build runner-declared source paths under the runner output directory. </summary>
    internal static class BuildRunnerOutputSourcePathResolver
    {
        /// <summary> Attempts to resolve one output-directory-relative source path to a full path. </summary>
        public static bool TryResolve (
            string outputDirectory,
            string relativePath,
            out string sourcePath)
        {
            sourcePath = string.Empty;
            if (!RelativePathContract.TryNormalize(relativePath, out var normalizedRelativePath))
            {
                return false;
            }

            var result = RepositoryPathNormalizer.TryNormalize(outputDirectory, normalizedRelativePath);
            if (!result.IsSuccess)
            {
                return false;
            }

            sourcePath = result.FullPath!;
            return true;
        }
    }
}
