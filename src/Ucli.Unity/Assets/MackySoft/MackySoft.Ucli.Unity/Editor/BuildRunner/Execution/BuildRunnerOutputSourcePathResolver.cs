using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
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
            [NotNullWhen(true)] out BuildRunnerOutputPath? outputPath,
            out string sourcePath)
        {
            outputPath = null;
            sourcePath = string.Empty;
            if (!BuildRunnerOutputPath.TryParse(relativePath, out var normalizedOutputPath))
            {
                return false;
            }

            var result = RepositoryPathNormalizer.TryNormalize(outputDirectory, normalizedOutputPath.Value);
            if (!result.IsSuccess)
            {
                return false;
            }

            outputPath = normalizedOutputPath;
            sourcePath = result.FullPath!;
            return true;
        }
    }
}
