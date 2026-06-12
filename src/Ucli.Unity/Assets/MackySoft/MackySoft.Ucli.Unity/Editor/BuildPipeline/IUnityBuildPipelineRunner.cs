using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Runs Unity BuildPipeline for one resolved build invocation. </summary>
    internal interface IUnityBuildPipelineRunner
    {
        /// <summary> Runs BuildPipeline and returns the produced BuildReport. </summary>
        /// <param name="options"> The BuildPipeline options. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
        /// <returns> The Unity BuildReport returned by BuildPipeline. </returns>
        ValueTask<BuildReport> RunAsync (
            BuildPlayerOptions options,
            CancellationToken cancellationToken);
    }
}
