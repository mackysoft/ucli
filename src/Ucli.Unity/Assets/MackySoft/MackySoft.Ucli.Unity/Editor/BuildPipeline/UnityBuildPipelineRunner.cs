using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Executes Unity's built-in BuildPipeline. </summary>
    internal sealed class UnityBuildPipelineRunner : IUnityBuildPipelineRunner
    {
        /// <inheritdoc />
        public ValueTask<BuildReport> RunAsync (
            BuildPlayerOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var report = BuildPipeline.BuildPlayer(options);
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<BuildReport>(report);
        }
    }
}
