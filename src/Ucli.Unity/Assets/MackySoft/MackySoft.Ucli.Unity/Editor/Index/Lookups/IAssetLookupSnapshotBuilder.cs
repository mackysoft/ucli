using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds one live asset lookup snapshot used by <c>index.assets.read</c>. </summary>
    internal interface IAssetLookupSnapshotBuilder
    {
        /// <summary> Builds one live asset lookup snapshot from persistent main assets under <c>Assets/</c>. </summary>
        ValueTask<IpcIndexAssetsReadResponse> BuildAsync (CancellationToken cancellationToken = default);
    }
}
