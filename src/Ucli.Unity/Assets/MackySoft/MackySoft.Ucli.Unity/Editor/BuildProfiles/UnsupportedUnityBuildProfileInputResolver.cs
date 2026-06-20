using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Rejects Unity Build Profile input on Unity versions that do not expose Build Profile APIs. </summary>
    internal sealed class UnsupportedUnityBuildProfileInputResolver : IUnityBuildProfileInputResolver
    {
        /// <inheritdoc />
        public Task<UnityBuildProfileInputResolutionResult> ResolveAsync (
            IpcBuildRunRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(UnityBuildProfileInputResolutionResult.Failure(
                new IpcError(
                    BuildErrorCodes.BuildUnityBuildProfileInvalid,
                    "Unity Build Profile input requires Unity 6000.0 or newer.",
                    null),
                request.UnityBuildProfile));
        }
    }
}
