using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Runs BuildPipeline for a Unity Build Profile asset input. </summary>
    internal interface IUnityBuildProfileBuildRunner
    {
        /// <summary> Runs the Unity Build Profile build and returns the normalized BuildReport artifact. </summary>
        IpcBuildReportArtifact? Run (
            IpcUnityBuildProfileInput unityBuildProfile,
            UnityBuildResolvedInput resolvedInput,
            IpcBuildOutputLayout outputLayout);
    }
}
