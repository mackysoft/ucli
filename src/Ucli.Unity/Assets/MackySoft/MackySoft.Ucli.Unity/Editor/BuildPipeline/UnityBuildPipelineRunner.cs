using UnityEditor;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Executes Unity's built-in BuildPipeline. </summary>
    internal sealed class UnityBuildPipelineRunner : IUnityBuildPipelineRunner
    {
        /// <inheritdoc />
        public IpcBuildReportArtifact? Run (BuildPlayerOptions options)
        {
            var report = BuildPipeline.BuildPlayer(options);
            return report == null ? null : UnityBuildReportNormalizer.Normalize(report);
        }
    }
}
