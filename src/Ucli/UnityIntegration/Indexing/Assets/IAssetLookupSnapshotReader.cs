using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Project;

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
        CancellationToken cancellationToken = default);
}