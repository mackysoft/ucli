using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Runs Unity BuildPipeline for one resolved build invocation. </summary>
    internal interface IUnityBuildPipelineRunner
    {
        /// <summary> Runs BuildPipeline and returns the produced BuildReport. </summary>
        /// <param name="options"> The BuildPipeline options. </param>
        /// <returns> The Unity BuildReport returned by BuildPipeline. </returns>
        BuildReport Run (BuildPlayerOptions options);
    }
}
