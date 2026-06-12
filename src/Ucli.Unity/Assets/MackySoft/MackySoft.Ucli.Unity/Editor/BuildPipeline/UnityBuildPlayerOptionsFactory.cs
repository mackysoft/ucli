using System;
using System.IO;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Creates Unity BuildPipeline options from resolved build input. </summary>
    internal static class UnityBuildPlayerOptionsFactory
    {
        /// <summary> Creates BuildPipeline options for one <c>build.run</c> request. </summary>
        public static BuildPlayerOptions Create (
            IpcBuildRunRequest request,
            UnityBuildResolvedInput resolvedInput)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (resolvedInput == null)
            {
                throw new ArgumentNullException(nameof(resolvedInput));
            }

            return new BuildPlayerOptions
            {
                scenes = resolvedInput.ScenePaths,
                target = resolvedInput.Target,
                targetGroup = resolvedInput.TargetGroup,
                options = resolvedInput.Options,
                locationPathName = ResolveLocationPathName(request.OutputPath, resolvedInput.Target),
            };
        }

        private static string ResolveLocationPathName (
            string outputDirectory,
            BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneOSX => Path.Combine(outputDirectory, "build.app"),
                BuildTarget.StandaloneWindows => Path.Combine(outputDirectory, "build.exe"),
                BuildTarget.StandaloneWindows64 => Path.Combine(outputDirectory, "build.exe"),
                BuildTarget.StandaloneLinux64 => Path.Combine(outputDirectory, "build"),
                BuildTarget.Android => Path.Combine(outputDirectory, "build.apk"),
                BuildTarget.WebGL => outputDirectory,
                BuildTarget.iOS => outputDirectory,
                BuildTarget.tvOS => outputDirectory,
                _ => Path.Combine(outputDirectory, "build"),
            };
        }
    }
}
