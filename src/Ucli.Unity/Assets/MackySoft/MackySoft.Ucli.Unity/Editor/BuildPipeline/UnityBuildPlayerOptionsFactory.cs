using System;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Creates Unity BuildPipeline options from resolved build input. </summary>
    internal static class UnityBuildPlayerOptionsFactory
    {
        /// <summary> Creates BuildPipeline options for one <c>build.run</c> request. </summary>
        public static BuildPlayerOptions Create (
            BuildRunExecutionRequest.ExplicitBuildPipeline request,
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
