using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds one scene-tree-lite snapshot for a specific scene asset. </summary>
    internal interface ISceneTreeLiteSnapshotBuilder
    {
        /// <summary> Builds one scene-tree-lite snapshot for the specified scene path. </summary>
        ValueTask<IpcIndexSceneTreeLiteReadResponse> BuildAsync (
            string scenePath,
            bool loadedSceneOnly = false,
            CancellationToken cancellationToken = default);
    }
}
