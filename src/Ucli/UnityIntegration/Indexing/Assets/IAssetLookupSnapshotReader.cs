using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Shared.Execution.Process;

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
