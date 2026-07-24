using System;
using System.IO;
using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Build;
using UnityEditor.Build.Reporting;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Writes Unity BuildReport objects as uCLI BuildReport JSON source files under the runner output directory. </summary>
    public static class UcliBuildReportSourceWriter
    {
        private static readonly UTF8Encoding Utf8NoBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary> Writes a BuildReport source file under the current runner context output directory. </summary>
        /// <param name="report"> The Unity BuildReport object to normalize. </param>
        /// <param name="relativePath"> The source file path relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        /// <returns> The BuildReport declaration to place on <see cref="UcliBuildRunnerResult" />. </returns>
        public static UcliBuildRunnerBuildReport Write (
            BuildReport report,
            string relativePath)
        {
            var context = UcliBuildRunnerContext.Current;
            if (context == null)
            {
                throw new InvalidOperationException("A uCLI build runner context is required to write BuildReport source files.");
            }

            return Write(context, report, relativePath);
        }

        /// <summary> Writes a BuildReport source file under the specified runner context output directory. </summary>
        /// <param name="context"> The runner context that owns the output directory. </param>
        /// <param name="report"> The Unity BuildReport object to normalize. </param>
        /// <param name="relativePath"> The source file path relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        /// <returns> The BuildReport declaration to place on <see cref="UcliBuildRunnerResult" />. </returns>
        public static UcliBuildRunnerBuildReport Write (
            UcliBuildRunnerContext context,
            BuildReport report,
            string relativePath)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            return WriteSourceJson(context, UnityBuildReportNormalizer.Normalize(report), relativePath);
        }

        internal static UcliBuildRunnerBuildReport WriteSourceJson (
            UcliBuildRunnerContext context,
            IpcBuildReportArtifact sourceArtifact,
            string relativePath)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (sourceArtifact == null)
            {
                throw new ArgumentNullException(nameof(sourceArtifact));
            }

            var outputDirectory = AbsolutePath.Parse(context.OutputDir);
            if (!BuildRunnerOutputSourcePathResolver.TryResolve(
                    outputDirectory,
                    relativePath,
                    out _,
                    out var sourcePath))
            {
                throw new ArgumentException("BuildReport source path must be a valid OutputDir-relative file path.", nameof(relativePath));
            }

            var json = JsonSerializer.Serialize(sourceArtifact, IpcJsonSerializerOptions.Default);
            if (!sourcePath.TryGetParent(out var directoryPath))
            {
                throw new InvalidOperationException($"BuildReport source directory could not be resolved: {sourcePath}");
            }

            Directory.CreateDirectory(directoryPath.Value);
            File.WriteAllText(sourcePath.Value, json, Utf8NoBomEncoding);
            return new UcliBuildRunnerBuildReport(relativePath);
        }

    }
}
