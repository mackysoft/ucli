#if UNITY_6000_0_OR_NEWER
using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEditor.Build.Profile;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Runs BuildPipeline with Unity 6000 Build Profile options. </summary>
    internal sealed class Unity6000BuildProfileBuildRunner : IUnityBuildProfileBuildRunner
    {
        /// <inheritdoc />
        public IpcBuildReportArtifact? Run (
            IpcBuildRunRequest request,
            UnityBuildResolvedInput resolvedInput,
            IpcBuildOutputLayout outputLayout)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (resolvedInput == null)
            {
                throw new ArgumentNullException(nameof(resolvedInput));
            }

            if (outputLayout == null)
            {
                throw new ArgumentNullException(nameof(outputLayout));
            }

            if (request.UnityBuildProfile == null)
            {
                throw new InvalidOperationException("Unity Build Profile input is missing.");
            }

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(request.UnityBuildProfile.Path);
            if (profile == null)
            {
                throw new InvalidOperationException($"Unity Build Profile asset could not be resolved: {request.UnityBuildProfile.Path}.");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = outputLayout.LocationPathName,
                options = resolvedInput.Options,
            });
            return report == null ? null : UnityBuildReportNormalizer.Normalize(report);
        }
    }
}
#endif
