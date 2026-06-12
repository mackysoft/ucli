using UnityEditor;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Runs Unity BuildPipeline for one resolved build invocation. </summary>
    internal interface IUnityBuildPipelineRunner
    {
        /// <summary> Runs BuildPipeline and returns the normalized produced BuildReport artifact. </summary>
        /// <param name="options"> The BuildPipeline options. </param>
        /// <returns> The normalized Unity BuildReport artifact, or <see langword="null" /> when BuildPipeline returned no report. </returns>
        IpcBuildReportArtifact? Run (BuildPlayerOptions options);
    }
}
