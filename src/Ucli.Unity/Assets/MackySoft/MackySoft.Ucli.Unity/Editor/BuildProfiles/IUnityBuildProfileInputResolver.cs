using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves Unity Build Profile asset inputs into BuildPipeline-ready inputs. </summary>
    internal interface IUnityBuildProfileInputResolver
    {
        /// <summary> Resolves and applies the Unity Build Profile input from a build.run request. </summary>
        Task<UnityBuildProfileInputResolutionResult> ResolveAsync (
            BuildRunExecutionRequest.UnityBuildProfile request,
            CancellationToken cancellationToken);
    }
}
