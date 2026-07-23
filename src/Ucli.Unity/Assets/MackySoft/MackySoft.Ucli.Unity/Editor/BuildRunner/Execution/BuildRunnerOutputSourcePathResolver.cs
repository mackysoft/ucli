using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
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
            AbsolutePath outputDirectory,
            string relativePath,
            [NotNullWhen(true)] out BuildRunnerOutputPath? outputPath,
            [NotNullWhen(true)] out AbsolutePath? sourcePath)
        {
            outputPath = null;
            sourcePath = null;
            if (!BuildRunnerOutputPath.TryParse(relativePath, out var normalizedOutputPath))
            {
                return false;
            }

            if (outputDirectory == null)
            {
                return false;
            }

            var containedPath = ContainedPath.Create(
                outputDirectory,
                BuildRunnerOutputPathAdapter.ToRootRelativePath(normalizedOutputPath));
            outputPath = normalizedOutputPath;
            sourcePath = containedPath.Target;
            return true;
        }
    }
}
