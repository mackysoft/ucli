using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Reads one live asset lookup snapshot through Unity IPC. </summary>
internal interface IAssetLookupSnapshotReader
{
    /// <summary> Reads one live asset lookup snapshot from Unity. </summary>
    ValueTask<AssetLookupSnapshotFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
