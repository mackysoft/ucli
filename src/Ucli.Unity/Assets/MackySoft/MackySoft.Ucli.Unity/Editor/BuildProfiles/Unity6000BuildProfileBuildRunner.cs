#if UNITY_6000_0_OR_NEWER
using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Runs BuildPipeline with Unity 6000 Build Profile options. </summary>
    internal sealed class Unity6000BuildProfileBuildRunner : IUnityBuildProfileBuildRunner
    {
        /// <inheritdoc />
        public IpcBuildReportArtifact? Run (
            IpcUnityBuildProfileInput unityBuildProfile,
            UnityBuildResolvedInput resolvedInput,
            ResolvedBuildPipelineOutputLayout outputLayout)
        {
            if (unityBuildProfile == null)
            {
                throw new ArgumentNullException(nameof(unityBuildProfile));
            }

            if (resolvedInput == null)
            {
                throw new ArgumentNullException(nameof(resolvedInput));
            }

            if (outputLayout == null)
            {
                throw new ArgumentNullException(nameof(outputLayout));
            }

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(unityBuildProfile.Path.Value);
            if (profile == null)
            {
                throw new UnityBuildProfileInputException($"Unity Build Profile asset could not be resolved: {unityBuildProfile.Path}.");
            }

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = outputLayout.LocationPath.Value,
                    options = resolvedInput.Options,
                });
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                throw new UnityBuildProfileInputException(
                    $"Unity Build Profile build could not be started from asset: {unityBuildProfile.Path}.",
                    exception);
            }

            return report == null ? null : UnityBuildReportNormalizer.Normalize(report);
        }
    }
}
#endif
