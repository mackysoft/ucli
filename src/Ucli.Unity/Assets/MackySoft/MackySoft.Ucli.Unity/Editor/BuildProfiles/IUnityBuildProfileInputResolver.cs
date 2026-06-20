using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves Unity Build Profile asset inputs into BuildPipeline-ready inputs. </summary>
    internal interface IUnityBuildProfileInputResolver
    {
        /// <summary> Resolves and applies the Unity Build Profile input from a build.run request. </summary>
        Task<UnityBuildProfileInputResolutionResult> ResolveAsync (
            IpcBuildRunRequest request,
            CancellationToken cancellationToken = default);
    }
}
