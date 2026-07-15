using System;
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

            if (request.OutputLayout == null)
            {
                throw new ArgumentException("BuildPipeline outputLayout must be specified.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.OutputLayout.LocationPathName))
            {
                throw new ArgumentException("BuildPipeline outputLayout.locationPathName must not be empty.", nameof(request));
            }

            var scenePaths = new string[resolvedInput.ScenePaths.Length];
            for (var i = 0; i < resolvedInput.ScenePaths.Length; i++)
            {
                scenePaths[i] = resolvedInput.ScenePaths[i].Value;
            }

            return new BuildPlayerOptions
            {
                scenes = scenePaths,
                target = resolvedInput.UnityBuildTarget,
                targetGroup = resolvedInput.UnityBuildTargetGroup,
                options = resolvedInput.Options,
                locationPathName = request.OutputLayout.LocationPathName,
            };
        }
    }
}
