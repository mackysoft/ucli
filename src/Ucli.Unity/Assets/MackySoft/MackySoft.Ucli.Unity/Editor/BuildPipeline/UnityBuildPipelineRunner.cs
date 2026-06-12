using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Executes Unity's built-in BuildPipeline. </summary>
    internal sealed class UnityBuildPipelineRunner : IUnityBuildPipelineRunner
    {
        /// <inheritdoc />
        public BuildReport Run (BuildPlayerOptions options)
        {
            return BuildPipeline.BuildPlayer(options);
        }
    }
}
