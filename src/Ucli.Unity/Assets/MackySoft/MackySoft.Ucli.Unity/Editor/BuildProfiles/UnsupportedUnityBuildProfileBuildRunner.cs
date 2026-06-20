using System;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Rejects Unity Build Profile BuildPipeline execution on unsupported Unity versions. </summary>
    internal sealed class UnsupportedUnityBuildProfileBuildRunner : IUnityBuildProfileBuildRunner
    {
        /// <inheritdoc />
        public IpcBuildReportArtifact? Run (
            IpcUnityBuildProfileInput unityBuildProfile,
            UnityBuildResolvedInput resolvedInput,
            IpcBuildOutputLayout outputLayout)
        {
            throw new InvalidOperationException("Unity Build Profile input requires Unity 6000.0 or newer.");
        }
    }
}
